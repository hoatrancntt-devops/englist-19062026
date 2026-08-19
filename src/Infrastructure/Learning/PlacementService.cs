using System.Text.Json;
using System.Text.Json.Serialization;
using EnglishForIT.Application.Content;
using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Learning;

/// <summary>Một câu trong đề, phần học viên nhìn thấy. Đáp án ở lại máy chủ.</summary>
public record PlacementCard(
    string ItemCode,
    string Kind,
    string? Skill,
    JsonElement Prompt);

public record PlacementSession(
    Guid AttemptId,
    string FormCode,
    string TitleVi,
    IReadOnlyList<PlacementCard> Cards,
    DateTimeOffset DeadlineAt,

    /// <summary>Mã các câu đã trả lời. Mở lại giữa chừng thì nhảy đúng chỗ đang dở.</summary>
    IReadOnlyList<string> AnsweredItemCodes,

    /// <summary>True khi đây là lượt thi cũ được mở lại chứ không phải lượt mới.</summary>
    bool Resumed,

    string MessageVi);

public record PlacementProgress(int Answered, int Total, DateTimeOffset DeadlineAt);

public record PlacementResult(
    Guid AttemptId,
    string FormCode,
    string Band,
    string Level,
    string SuggestedLayer,
    Dictionary<string, double> SkillScores,
    IReadOnlyList<string> UnmeasuredSkills,
    double VocabGrammarScore,
    double OverallScore,
    double FastAnswerRatio,
    double SelfRatedScore,
    int Answered,
    int Total,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<string> NotesVi,
    string SummaryVi);

/// <summary>
/// Bài xếp lớp.
///
/// Bốn quyết định định hình lớp này:
///
/// Một, <b>đáp án không bao giờ rời máy chủ</b>. Chấm ngay khi nhận từng câu và lưu điểm,
/// nhưng không trả điểm về client cho tới lúc nộp toàn bài. Trả sớm thì mở DevTools là
/// biết mình sai câu nào rồi thi lại đề còn lại với đáp án trong tay.
///
/// Hai, <b>chấm từng câu chứ không gom cuối buổi</b>. Mất mạng giữa chừng thì phần đã làm
/// vẫn còn. Mười tám phút làm lại từ đầu là lý do chính khiến người ta bỏ bài xếp lớp.
///
/// Ba, <b>thi lại thì đổi đề</b>. Cùng một đề lần hai cho điểm cao hơn vì nhớ đáp án,
/// không phải vì giỏi lên, và cả lộ trình sau đó đặt sai chỗ.
///
/// Bốn, <b>trục không đo được thì nói là không đo được</b>, không âm thầm cho 0 điểm.
/// </summary>
public class PlacementService(
    AppDbContext db,
    IOptions<LearningPolicyOptions> policy,
    ILogger<PlacementService> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },

        // Phải khớp với PlacementSeeder: nó ghi PromptJson và AnswerJson, lớp này đọc lại.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private LearningPolicyOptions Policy => policy.Value;

    /// <summary>
    /// Bắt đầu hoặc mở lại một lượt thi.
    ///
    /// Trả null khi chưa có đề nào đang bật — chưa seed nội dung thì không phải lỗi,
    /// chỉ là chưa xếp lớp được.
    /// </summary>
    public async Task<PlacementSession?> StartAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var open = await db.PlacementAttempts
            .Include(a => a.Form!).ThenInclude(f => f.Items)
            .Where(a => a.UserId == userId && a.Status == PlacementAttemptStatus.InProgress)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (open is not null)
        {
            if (open.DeadlineAt > now)
            {
                var answered = await db.PlacementAnswers
                    .Where(x => x.AttemptId == open.Id)
                    .Join(db.PlacementFormItems, x => x.ItemId, i => i.Id, (_, i) => i.Code)
                    .ToListAsync(ct);

                return BuildSession(open, answered, resumed: true);
            }

            // Quá hạn mà chưa nộp: đóng lại rồi cho làm đề mới. Để nguyên trạng
            // InProgress sẽ khiến người học kẹt vĩnh viễn ở một lượt không nộp được.
            open.Status = PlacementAttemptStatus.Abandoned;
            logger.LogInformation("Lượt xếp lớp {Attempt} quá hạn, đã đóng.", open.Id);
        }

        var form = await PickFormAsync(userId, ct);

        if (form is null)
        {
            return null;
        }

        var attempt = new PlacementAttempt
        {
            UserId = userId,
            FormId = form.Id,
            Form = form,
            StartedAt = now,
            DeadlineAt = now.AddMinutes(form.EstimatedMinutes + Policy.PlacementGraceMinutes),
        };

        db.PlacementAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        return BuildSession(attempt, [], resumed: false);
    }

    /// <summary>
    /// Chọn đề cho lượt mới: ưu tiên đề học viên chưa từng nộp.
    ///
    /// Hết đề mới thì lấy đề nộp lâu nhất. Chặn hẳn việc thi lại sẽ tệ hơn: người
    /// học lần đầu bị đặt sai vì hôm đó mệt sẽ không có đường sửa.
    /// </summary>
    private async Task<Domain.Entities.Content.PlacementForm?> PickFormAsync(Guid userId, CancellationToken ct)
    {
        var forms = await db.PlacementForms
            .Include(f => f.Items)
            .Where(f => f.IsActive)
            .OrderBy(f => f.Code)
            .ToListAsync(ct);

        if (forms.Count == 0)
        {
            logger.LogWarning("Không có đề xếp lớp nào đang bật.");
            return null;
        }

        var lastUsed = await db.PlacementAttempts
            .Where(a => a.UserId == userId && a.Status == PlacementAttemptStatus.Submitted)
            .GroupBy(a => a.FormId)
            .Select(g => new { FormId = g.Key, Last = g.Max(a => a.SubmittedAt) })
            .ToDictionaryAsync(x => x.FormId, x => x.Last, ct);

        var unused = forms.Where(f => !lastUsed.ContainsKey(f.Id)).ToList();

        if (unused.Count > 0)
        {
            return unused[0];
        }

        return forms.OrderBy(f => lastUsed[f.Id] ?? DateTimeOffset.MinValue).First();
    }

    private PlacementSession BuildSession(PlacementAttempt attempt, IReadOnlyList<string> answered, bool resumed)
    {
        var cards = ServedItems(attempt.Form!.Items)
            .Select(i => new PlacementCard(
                i.Code,
                i.Kind.ToString(),
                i.Skill?.ToString(),
                JsonSerializer.Deserialize<JsonElement>(i.PromptJson)))
            .ToList();

        var message = resumed
            ? "Bạn đang có một lượt làm dở. Tiếp tục từ chỗ đã dừng."
            : $"{cards.Count} câu. Làm liền một mạch sẽ chính xác hơn, nhưng dừng giữa chừng cũng không mất bài.";

        return new PlacementSession(
            attempt.Id,
            attempt.Form.Code,
            attempt.Form.TitleVi,
            cards,
            attempt.DeadlineAt,
            answered,
            resumed,
            message);
    }

    /// <summary>
    /// Các câu thật sự đưa ra cho học viên.
    ///
    /// Câu nói bị lọc ở đúng một chỗ này khi chưa bật cờ, để không có đường nào khác
    /// làm rò chúng ra giao diện.
    /// </summary>
    private IEnumerable<Domain.Entities.Content.PlacementFormItem> ServedItems(
        IEnumerable<Domain.Entities.Content.PlacementFormItem> items)
    {
        var ordered = items.OrderBy(i => i.OrderIndex);

        return Policy.PlacementSpeakingEnabled
            ? ordered
            : ordered.Where(i => i.Kind is not (PlacementItemKind.ReadAloud or PlacementItemKind.Repeat));
    }

    /// <summary>
    /// Nhận một câu trả lời: chấm ngay, lưu điểm, nhưng chỉ trả về tiến độ.
    ///
    /// Trả null khi lượt thi không thuộc học viên này, đã nộp, hoặc mã câu không nằm
    /// trong đề — cùng một null cho cả ba, không nói rõ vì lý do nào.
    /// </summary>
    public async Task<PlacementProgress?> SubmitAnswerAsync(
        Guid userId,
        Guid attemptId,
        string itemCode,
        JsonElement response,
        int responseSeconds,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var attempt = await db.PlacementAttempts
            .Include(a => a.Form!).ThenInclude(f => f.Items)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct);

        if (attempt is null || attempt.Status != PlacementAttemptStatus.InProgress)
        {
            return null;
        }

        var item = ServedItems(attempt.Form!.Items)
            .FirstOrDefault(i => i.Code == itemCode);

        if (item is null)
        {
            return null;
        }

        // Quá hạn thì không nhận thêm câu nữa, nhưng phần đã trả lời vẫn chấm được.
        if (attempt.DeadlineAt <= now)
        {
            return await ProgressAsync(attempt, ct);
        }

        var parsed = ParseResponse(response);
        var answerDoc = JsonSerializer.Deserialize<PlacementAnswerDocument>(item.AnswerJson, Json)
                        ?? new PlacementAnswerDocument();

        var score = PlacementScoring.Grade(item.Kind, answerDoc, parsed);

        var existing = await db.PlacementAnswers
            .FirstOrDefaultAsync(x => x.AttemptId == attempt.Id && x.ItemId == item.Id, ct);

        if (existing is null)
        {
            db.PlacementAnswers.Add(new PlacementAnswer
            {
                AttemptId = attempt.Id,
                ItemId = item.Id,
                ResponseJson = response.GetRawText(),
                Score = score ?? 0,
                IsCorrect = score >= 100,
                ResponseSeconds = responseSeconds,
            });
        }
        else
        {
            // Sửa lại câu đã trả lời được phép: học viên quay lại câu trước là chuyện thường.
            existing.ResponseJson = response.GetRawText();
            existing.Score = score ?? 0;
            existing.IsCorrect = score >= 100;
            existing.ResponseSeconds = responseSeconds;
        }

        await db.SaveChangesAsync(ct);

        return await ProgressAsync(attempt, ct);
    }

    private async Task<PlacementProgress> ProgressAsync(PlacementAttempt attempt, CancellationToken ct)
    {
        var answered = await db.PlacementAnswers.CountAsync(x => x.AttemptId == attempt.Id, ct);
        return new PlacementProgress(answered, ServedItems(attempt.Form!.Items).Count(), attempt.DeadlineAt);
    }

    /// <summary>
    /// Nộp bài: tổng hợp điểm, quy ra bậc, ghi vào hồ sơ học.
    ///
    /// Câu không trả lời tính 0 điểm — bỏ qua chúng sẽ khiến người làm ba câu rồi nộp
    /// ra kết quả cao hơn người làm hết hai mươi hai câu.
    /// </summary>
    public async Task<PlacementResult?> SubmitAsync(
        Guid userId,
        Guid attemptId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var attempt = await db.PlacementAttempts
            .Include(a => a.Form!).ThenInclude(f => f.Items)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct);

        if (attempt is null)
        {
            return null;
        }

        if (attempt.Status == PlacementAttemptStatus.Submitted)
        {
            return await BuildResultAsync(attempt, ct);
        }

        var served = ServedItems(attempt.Form!.Items).ToList();

        var answers = await db.PlacementAnswers
            .Where(x => x.AttemptId == attempt.Id)
            .ToDictionaryAsync(x => x.ItemId, ct);

        var scored = new List<ScoredItem>();

        foreach (var item in served)
        {
            answers.TryGetValue(item.Id, out var answer);

            int? selfIndex = null;
            int? choiceCount = null;

            if (item.Kind == PlacementItemKind.Likert && answer is not null)
            {
                // Chỉ câu Likert bật self_rating mới có thang tăng dần để quy ra điểm.
                // Câu "bạn yếu nhất kỹ năng nào" cũng là Likert năm lựa chọn nhưng các
                // lựa chọn không xếp cao thấp, nên phải đọc cờ chứ không suy từ hình dạng.
                var rule = JsonSerializer.Deserialize<PlacementAnswerDocument>(item.AnswerJson, Json);
                var prompt = JsonSerializer.Deserialize<PlacementPromptDocument>(item.PromptJson, Json);

                if (rule?.SelfRating == true && prompt?.Choices is { Count: > 1 } choices)
                {
                    selfIndex = ParseResponse(JsonSerializer.Deserialize<JsonElement>(answer.ResponseJson)).ChoiceIndex;
                    choiceCount = choices.Count;
                }
            }

            scored.Add(new ScoredItem(
                ItemCode: item.Code,
                Kind: item.Kind,
                Skill: item.Skill,
                Weight: item.Weight,
                Score: answer?.Score ?? 0,
                AnsweredFast: answer is not null &&
                              PlacementScoring.IsFastAnswer(answer.ResponseSeconds, item.SlowAnswerSeconds),
                SelfRatingIndex: selfIndex,
                SelfRatingChoiceCount: choiceCount));
        }

        var outcome = PlacementScoring.Summarise(scored);

        attempt.Status = PlacementAttemptStatus.Submitted;
        attempt.SubmittedAt = now;
        attempt.ResultLevel = outcome.Level;
        attempt.SkillScores = outcome.SkillScores;
        attempt.VocabGrammarScore = outcome.VocabGrammarScore;
        attempt.FastAnswerRatio = outcome.FastAnswerRatio;
        attempt.SelfRatedScore = outcome.SelfRatedScore;
        attempt.ExplanationJson = JsonSerializer.Serialize(new
        {
            outcome.Band,
            outcome.OverallScore,
            UnmeasuredSkills = outcome.UnmeasuredSkills.Select(s => s.ToString()).ToList(),
            outcome.NotesVi,
        }, Json);

        // Hồ sơ học là thứ engine chống nhảy cóc đọc. Không ghi vào đây thì thi xong
        // lộ trình vẫn y như cũ và cả bài thi thành vô nghĩa.
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile is not null)
        {
            profile.CurrentLevel = outcome.Level;
            profile.CurrentLayer = outcome.SuggestedLayer;
        }
        else
        {
            logger.LogWarning("Học viên {User} nộp xếp lớp nhưng không có hồ sơ học để ghi kết quả.", userId);
        }

        var unlocked = await GrantLevelUnlockAsync(userId, outcome.Level, now, ct);

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Học viên {User} xếp lớp xong: {Band}, tầng {Layer}, mở khoá {Unlocked} bài",
            userId, outcome.Band, outcome.SuggestedLayer, unlocked);

        return await BuildResultAsync(attempt, ct);
    }

    /// <summary>
    /// Ghi công cho mọi bài ở bậc THẤP HƠN bậc vừa đạt.
    ///
    /// Đây là đường tắt hợp lệ duy nhất của hệ thống. Thi vượt từng bài cố ý không mở khoá bài
    /// sau (xem <see cref="PrerequisiteEngine.Evaluate"/>), nên nếu không có chỗ này thì người
    /// đã biết tiếng Anh phải ngồi hết 28 bài vỡ lòng — và họ sẽ bỏ app chứ không ngồi.
    ///
    /// <b>Thấp hơn, không phải thấp hơn hoặc bằng.</b> Đạt B1 thì được ghi công 20 bài
    /// PreA1/A1/A2, còn 8 bài B1 tầng Văn phòng vẫn phải học. Vì <see cref="CefrLevel"/> dừng ở
    /// B1 nên 30 bài Professional (đều B1) không bao giờ tự mở — không phải nhờ may mắn mà nhờ
    /// bất đẳng thức này. Nếu sau này thêm bậc B2, hãy xem lại đúng dòng Where bên dưới trước
    /// khi thêm, không thì cả tầng chuyên môn mở toang.
    /// </summary>
    private async Task<int> GrantLevelUnlockAsync(
        Guid userId, CefrLevel achieved, DateTimeOffset now, CancellationToken ct)
    {
        // So sánh bậc TRONG BỘ NHỚ, không đưa phép so sánh xuống SQL.
        //
        // Cột level lưu dạng chuỗi, nên `l.Level < achieved` dịch thành so sánh chữ cái của
        // Postgres: 'A1' < 'B1' đúng, nhưng 'PreA1' < 'B1' SAI vì P đứng sau B. Bản đầu viết
        // như vậy và lặng lẽ bỏ sót đúng 5 bài vỡ lòng — không lỗi, không cảnh báo, chỉ là
        // người đã đạt B1 vẫn phải ngồi học đánh vần.
        var published = await db.Lessons
            .AsNoTracking()
            .Where(l => l.Status == ContentStatus.Published)
            .Select(l => new { l.Id, l.Level })
            .ToListAsync(ct);

        var below = published.Where(l => l.Level < achieved).Select(l => l.Id).ToList();

        if (below.Count == 0)
        {
            return 0;
        }

        // Không đụng tới bài đã có tiến độ. Học viên thi lại xếp lớp không được phép làm điểm
        // thật của họ tụt xuống mức ghi công, và cũng không được cộng thêm lần thứ hai.
        var already = await db.LessonMasteries
            .Where(m => m.UserId == userId && below.Contains(m.LessonId))
            .Select(m => m.LessonId)
            .ToListAsync(ct);

        var granted = below.Except(already).ToList();

        foreach (var lessonId in granted)
        {
            db.LessonMasteries.Add(new LessonMastery
            {
                UserId = userId,
                LessonId = lessonId,
                State = LessonState.Mastered,
                MasteryRaw = policy.Value.MasteryThreshold,
                MasteryEffective = policy.Value.MasteryThreshold,

                // Để trống điểm từng kỹ năng, cố ý. Bài xếp lớp đo trình độ chung chứ không đo
                // bài này, nên bịa ra điểm Nghe/Nói cho từng bài là nói dối học viên. Engine đã
                // bỏ qua kỹ năng không có điểm nên chỗ trống này an toàn.
                SkillScores = [],

                MasteredAt = now,
                LastActivityAt = now,
            });

            db.LessonStateEvents.Add(new LessonStateEvent
            {
                UserId = userId,
                LessonId = lessonId,
                FromState = LessonState.Locked,
                ToState = LessonState.Mastered,
                Reason = LessonStateReason.PlacementUnlock,
                DetailJson = JsonSerializer.Serialize(new
                {
                    achieved = achieved.ToString(),
                    credited = policy.Value.MasteryThreshold,
                }, Json),
            });
        }

        return granted.Count;
    }

    /// <summary>Kết quả lượt gần nhất đã nộp. Null khi học viên chưa từng thi xong.</summary>
    public async Task<PlacementResult?> GetLatestResultAsync(Guid userId, CancellationToken ct = default)
    {
        var attempt = await db.PlacementAttempts
            .Include(a => a.Form!).ThenInclude(f => f.Items)
            .Where(a => a.UserId == userId && a.Status == PlacementAttemptStatus.Submitted)
            .OrderByDescending(a => a.SubmittedAt)
            .FirstOrDefaultAsync(ct);

        return attempt is null ? null : await BuildResultAsync(attempt, ct);
    }

    private async Task<PlacementResult> BuildResultAsync(PlacementAttempt attempt, CancellationToken ct)
    {
        var served = ServedItems(attempt.Form!.Items).ToList();
        var answered = await db.PlacementAnswers.CountAsync(x => x.AttemptId == attempt.Id, ct);

        var explanation = string.IsNullOrWhiteSpace(attempt.ExplanationJson)
            ? null
            : JsonSerializer.Deserialize<StoredExplanation>(attempt.ExplanationJson, Json);

        var band = explanation?.Band ?? "L1";
        var notes = explanation?.NotesVi ?? [];
        var unmeasured = explanation?.UnmeasuredSkills ?? [];
        var overall = explanation?.OverallScore ?? 0;

        return new PlacementResult(
            AttemptId: attempt.Id,
            FormCode: attempt.Form.Code,
            Band: band,
            Level: (attempt.ResultLevel ?? CefrLevel.PreA1).ToString(),
            SuggestedLayer: SuggestedLayerOf(band).ToString(),
            SkillScores: attempt.SkillScores.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            UnmeasuredSkills: unmeasured,
            VocabGrammarScore: attempt.VocabGrammarScore,
            OverallScore: overall,
            FastAnswerRatio: attempt.FastAnswerRatio,
            SelfRatedScore: attempt.SelfRatedScore,
            Answered: answered,
            Total: served.Count,
            SubmittedAt: attempt.SubmittedAt ?? attempt.StartedAt,
            NotesVi: notes,
            SummaryVi: SummaryOf(band));
    }

    private record StoredExplanation(
        string Band,
        double OverallScore,
        List<string> UnmeasuredSkills,
        List<string> NotesVi);

    private static ContextLayer SuggestedLayerOf(string band) => band switch
    {
        "L3" => ContextLayer.Office,
        "L4" => ContextLayer.Professional,
        _ => ContextLayer.Life,
    };

    private static string SummaryOf(string band) => band switch
    {
        "L0" => "Bạn bắt đầu từ số 0. Lộ trình mở từ bài đầu tiên của tầng Đời sống — chữ cái, số, giờ và ba câu cứu hộ khi không hiểu.",
        "L1" => "Bạn nói được câu ngắn về bản thân và công việc. Lộ trình bắt đầu ở tầng Đời sống.",
        "L2" => "Bạn trao đổi được việc thường ngày. Lộ trình đi nốt tầng Đời sống rồi sang Văn phòng.",
        "L3" => "Bạn báo cáo được việc mình làm và đọc được email, ticket. Lộ trình bắt đầu ở tầng Văn phòng.",
        _ => "Bạn theo được họp kỹ thuật. Lộ trình bắt đầu ở tầng Chuyên môn.",
    };

    /// <summary>
    /// Đọc câu trả lời thô. Chấp nhận cả hai hình dạng client có thể gửi:
    /// chọn đáp án thì có choiceIndex, viết tay thì có text.
    /// </summary>
    private static PlacementScoring.Response ParseResponse(JsonElement response)
    {
        int? choiceIndex = null;
        string? text = null;

        if (response.ValueKind == JsonValueKind.Object)
        {
            if (response.TryGetProperty("choiceIndex", out var choice) &&
                choice.ValueKind == JsonValueKind.Number)
            {
                choiceIndex = choice.GetInt32();
            }

            if (response.TryGetProperty("text", out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                text = value.GetString();
            }
        }

        return new PlacementScoring.Response(choiceIndex, text);
    }
}
