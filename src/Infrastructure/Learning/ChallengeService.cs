using System.Text.Json;
using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Learning;

/// <summary>Một câu trong bài thi vượt. Chỉ có đề, không bao giờ có đáp án.</summary>
public record ChallengeItem(string Code, string Skill, int Difficulty, JsonElement Prompt);

public record ChallengeOffer(
    string LessonCode,
    string TitleVi,
    string ObjectiveVi,
    int PassThreshold,
    IReadOnlyList<ChallengeItem> Items,
    /// <summary>True thì client hiện đề. False thì chỉ hiện <see cref="ReasonVi"/>.</summary>
    bool Eligible,
    string ReasonVi,
    /// <summary>Có giá trị khi đang trong thời gian chờ sau một lần trượt.</summary>
    DateTimeOffset? RetryAt);

public record ChallengeResult(
    bool Passed,
    double Score,
    int PassThreshold,
    int CorrectCount,
    int TotalCount,
    /// <summary>Mã các câu làm sai. Học viên phải biết mình hổng chỗ nào.</summary>
    IReadOnlyList<string> WrongItemCodes,
    IReadOnlyList<string> SkillsBelowThreshold,
    int ReviewItemsScheduled,
    DateTimeOffset? RetryAt,
    string MessageVi);

/// <summary>
/// Thi vượt: qua một bài mà không phải học tuần tự.
///
/// Ba ràng buộc định hình dịch vụ này:
///
/// Một, ngưỡng cao hơn học thường (85 so với 80). Bỏ qua cả quá trình học thì phải chứng minh
/// nhiều hơn một chút, nếu không thi vượt trở thành đường vòng dễ hơn đường chính.
///
/// Hai, trượt thì phải chờ. Một bài chỉ có chừng mười câu; cho thi lại ngay là mời học viên
/// dò đáp án cho tới khi trúng, và cái họ chứng minh được khi đó là trí nhớ ngắn hạn.
///
/// Ba, qua rồi vẫn nợ ôn tập. Bảng <see cref="ChallengePass"/> tách riêng khỏi mastery đúng vì
/// việc này: người thi vượt chưa từng gặp các câu đó lần thứ hai, nên câu sai vào hàng đợi
/// ngay ngày mai, còn câu đúng hẹn xa hơn bình thường.
/// </summary>
public class ChallengeService(
    AppDbContext db,
    IOptions<LearningPolicyOptions> policyOptions,
    ILogger<ChallengeService> logger)
{
    private readonly LearningPolicyOptions _policy = policyOptions.Value;
    private readonly PrerequisiteEngine _engine = new(policyOptions.Value);
    private readonly ActivityGrader _grader = new();

    /// <summary>
    /// Xem một bài có cho thi vượt không, và nếu có thì trả về đề.
    ///
    /// Trả về <c>null</c> chỉ khi không có bài nào mang mã đó. Bài tồn tại nhưng không đủ điều kiện
    /// vẫn trả về đối tượng, với <see cref="ChallengeOffer.Eligible"/> false và lý do bằng tiếng Việt —
    /// giao diện cần nói được vì sao, không chỉ là nút bị mờ.
    /// </summary>
    public async Task<ChallengeOffer?> GetOfferAsync(
        Guid userId, string lessonCode, DateTimeOffset now, CancellationToken ct = default)
    {
        var lesson = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Activities)
                .ThenInclude(a => a.Items)
            .FirstOrDefaultAsync(l => l.Code == lessonCode && l.Status == ContentStatus.Published, ct);

        if (lesson is null)
        {
            return null;
        }

        var items = SelectItems(lesson);

        ChallengeOffer Refuse(string reason, DateTimeOffset? retryAt = null) => new(
            lesson.Code, lesson.TitleVi, lesson.ObjectiveVi, _policy.ChallengePassThreshold,
            [], false, reason, retryAt);

        if (items.Count < _policy.ChallengeMinItems)
        {
            return Refuse(
                $"Bài này chỉ có {items.Count} câu chấm được nên không mở thi vượt. " +
                "Ít câu quá thì đoán mò cũng trúng, kết quả không nói lên điều gì.");
        }

        var mastery = await db.LessonMasteries
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.LessonId == lesson.Id, ct);

        if (mastery?.State == LessonState.Mastered)
        {
            // Người đã thi vượt cũng rơi vào nhánh này, nhưng không được nói với họ là "đã thạo".
            // Cả lộ trình đang nói ngược lại — bài sau vẫn khoá vì họ CHƯA học bài này — nên câu
            // này mà nói "đã thạo" là chính hệ thống tự cãi nhau trong hai màn hình liền nhau.
            return Refuse(mastery.UnlockedByChallenge
                ? "Bạn đã thi vượt bài này rồi, không thi lại được. Muốn mở bài sau thì cần học bài này."
                : "Bạn đã thạo bài này rồi, không cần thi vượt.");
        }

        var alreadyPassed = await db.ChallengePasses
            .AsNoTracking()
            .AnyAsync(c => c.UserId == userId && c.LessonId == lesson.Id, ct);

        if (alreadyPassed)
        {
            return Refuse("Bạn đã qua bài này bằng thi vượt rồi.");
        }

        var retryAt = await RetryAvailableAtAsync(userId, lesson.Id, now, ct);

        if (retryAt is not null)
        {
            var hours = Math.Max(1, (int)Math.Ceiling((retryAt.Value - now).TotalHours));

            return Refuse(
                $"Bạn vừa trượt thi vượt bài này. Thi lại được sau khoảng {hours} tiếng nữa. " +
                "Trong lúc chờ, học bài theo cách thường vẫn nhanh hơn.",
                retryAt);
        }

        return new ChallengeOffer(
            lesson.Code,
            lesson.TitleVi,
            lesson.ObjectiveVi,
            _policy.ChallengePassThreshold,
            items.Select(s => new ChallengeItem(
                s.Item.Code, s.Skill.ToString(), s.Item.Difficulty, Parse(s.Item.PromptJson))).ToList(),
            true,
            $"Đúng từ {_policy.ChallengePassThreshold} điểm trở lên là qua bài, không phải học lại từ đầu. " +
            "Trượt thì chờ nửa ngày mới thi lại được.",
            null);
    }

    /// <summary>Chấm bài thi vượt và ghi hệ quả.</summary>
    public async Task<ChallengeResult?> SubmitAsync(
        Guid userId,
        string lessonCode,
        IReadOnlyList<ItemResponse> responses,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var lesson = await db.Lessons
            .Include(l => l.Activities)
                .ThenInclude(a => a.Items)
            .FirstOrDefaultAsync(l => l.Code == lessonCode && l.Status == ContentStatus.Published, ct);

        if (lesson is null)
        {
            return null;
        }

        // Kiểm lại điều kiện ngay trước khi chấm. Client có thể đã mở đề từ lâu, hoặc gọi
        // thẳng API bỏ qua bước xin đề.
        var offer = await GetOfferAsync(userId, lessonCode, now, ct);

        if (offer is null || !offer.Eligible)
        {
            return new ChallengeResult(
                false, 0, _policy.ChallengePassThreshold, 0, 0, [], [], 0,
                offer?.RetryAt,
                offer?.ReasonVi ?? "Bài này hiện không thi vượt được.");
        }

        var items = SelectItems(lesson);
        var answerKey = items.ToDictionary(s => s.Item.Code, s => s.Item.AnswerJson, StringComparer.Ordinal);

        var grade = _grader.GradeMultipleChoice(responses, answerKey, _policy.ChallengePassThreshold);

        var skillByCode = items.ToDictionary(s => s.Item.Code, s => s.Skill, StringComparer.Ordinal);

        var skillScores = _grader.AggregateSkillScores(
            [.. grade.Items.Select(g => (skillByCode[g.ItemCode], g.Correct ? 100.0 : 0.0))]);

        // Cùng một quy tắc trục yếu với học thường, gọi vào đúng một chỗ.
        var below = _engine.SkillsBelowThreshold(skillScores, lesson.SupportedSkills);

        var passed = grade.Score >= _policy.ChallengePassThreshold && below.Count == 0;
        var wrong = grade.Items.Where(g => !g.Correct).Select(g => g.ItemCode).ToList();

        var previousState = await db.LessonMasteries
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.LessonId == lesson.Id)
            .Select(m => (LessonState?)m.State)
            .FirstOrDefaultAsync(ct) ?? LessonState.Locked;

        var scheduled = 0;
        DateTimeOffset? retryAt = null;

        if (passed)
        {
            scheduled = await RecordPassAsync(userId, lesson, grade, skillScores, items, now, ct);
        }
        else
        {
            var nextTry = now.AddHours(_policy.ChallengeCooldownHours);
            retryAt = nextTry;
            RecordFailure(userId, lesson.Id, previousState, grade.Score, below, nextTry);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Học viên {UserId} thi vượt {Code}: {Score} điểm, {Verdict}",
            userId, lessonCode, grade.Score, passed ? "qua" : "trượt");

        return new ChallengeResult(
            passed,
            grade.Score,
            _policy.ChallengePassThreshold,
            grade.Items.Count(g => g.Correct),
            grade.Items.Count,
            wrong,
            [.. below.Select(s => s.ToString())],
            scheduled,
            retryAt,
            BuildMessage(passed, grade.Score, below, wrong.Count, grade.Items.Count));
    }

    /// <summary>
    /// Ghi hệ quả của một lần qua: vé thi vượt, mastery, nhật ký trạng thái, và nợ ôn tập.
    /// </summary>
    private async Task<int> RecordPassAsync(
        Guid userId,
        Domain.Entities.Content.Lesson lesson,
        ActivityGrade grade,
        Dictionary<SkillType, double> skillScores,
        IReadOnlyList<ChallengeSource> items,
        DateTimeOffset now,
        CancellationToken ct)
    {
        db.ChallengePasses.Add(new ChallengePass
        {
            UserId = userId,
            LessonId = lesson.Id,
            Score = grade.Score,
            PassedAt = now,
            ItemCodesJson = JsonSerializer.Serialize(items.Select(s => s.Item.Code)),
        });

        var mastery = await db.LessonMasteries
            .FirstOrDefaultAsync(m => m.UserId == userId && m.LessonId == lesson.Id, ct);

        var previousState = mastery?.State ?? LessonState.Locked;

        if (mastery is null)
        {
            mastery = new LessonMastery { UserId = userId, LessonId = lesson.Id };
            db.LessonMasteries.Add(mastery);
        }

        mastery.State = LessonState.Mastered;
        mastery.MasteryRaw = grade.Score;
        mastery.MasteryEffective = grade.Score;
        mastery.SkillScores = skillScores;
        mastery.MasteredAt = now;
        mastery.LastActivityAt = now;
        mastery.AttemptsCount++;
        mastery.UnlockedByChallenge = true;

        db.LessonStateEvents.Add(new LessonStateEvent
        {
            UserId = userId,
            LessonId = lesson.Id,
            FromState = previousState,
            ToState = LessonState.Mastered,
            Reason = LessonStateReason.ChallengePassed,
            DetailJson = JsonSerializer.Serialize(new
            {
                score = grade.Score,
                threshold = _policy.ChallengePassThreshold,
                skillScores = skillScores.ToDictionary(k => k.Key.ToString(), v => v.Value),
                wrong = grade.Items.Where(g => !g.Correct).Select(g => g.ItemCode),
            }),
        });

        return await ScheduleReviewsAsync(userId, grade, items, now, ct);
    }

    /// <summary>
    /// Ghi một lần trượt.
    ///
    /// Trạng thái bài giữ nguyên — trượt thi vượt không được phép làm học viên tệ hơn lúc
    /// chưa thi. Bản ghi này tồn tại chỉ để tính khoảng chờ.
    /// </summary>
    private void RecordFailure(
        Guid userId,
        Guid lessonId,
        LessonState currentState,
        double score,
        IReadOnlyList<SkillType> below,
        DateTimeOffset retryAt)
    {
        db.LessonStateEvents.Add(new LessonStateEvent
        {
            UserId = userId,
            LessonId = lessonId,
            FromState = currentState,
            ToState = currentState,
            Reason = LessonStateReason.ChallengeFailed,
            DetailJson = JsonSerializer.Serialize(new
            {
                score,
                threshold = _policy.ChallengePassThreshold,
                below = below.Select(s => s.ToString()),
                retryAt,
            }),
        });
    }

    /// <summary>
    /// Xếp lịch ôn cho người vừa thi vượt.
    ///
    /// Khác học thường ở chỗ câu đúng hẹn xa hơn (7 ngày thay vì 1): họ vừa chứng minh
    /// biết rồi, bắt ôn ngay ngày mai là phí thời gian của họ. Câu sai vẫn về 1 ngày —
    /// đó đúng là những chỗ họ hổng, và họ chưa từng học chúng qua bài giảng.
    /// </summary>
    private async Task<int> ScheduleReviewsAsync(
        Guid userId,
        ActivityGrade grade,
        IReadOnlyList<ChallengeSource> items,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var idByCode = items.ToDictionary(s => s.Item.Code, s => s.Item.Id, StringComparer.Ordinal);

        var existing = await db.ReviewQueue
            .Where(r => r.UserId == userId && idByCode.Values.Contains(r.ItemId))
            .ToDictionaryAsync(r => r.ItemId, ct);

        var scheduled = 0;

        foreach (var graded in grade.Items)
        {
            if (!idByCode.TryGetValue(graded.ItemCode, out var itemId))
            {
                continue;
            }

            if (!existing.TryGetValue(itemId, out var row))
            {
                row = new ReviewQueueItem { UserId = userId, ItemId = itemId, Ease = 2.5 };
                db.ReviewQueue.Add(row);
            }

            if (graded.Correct)
            {
                row.RepetitionCount++;
                row.IntervalDays = Math.Clamp(Math.Max(row.IntervalDays, 7), 1, 60);
            }
            else
            {
                row.LapseCount++;
                row.IntervalDays = 1;
                row.Ease = Math.Max(1.3, row.Ease - 0.2);
            }

            row.DueAt = now.AddDays(row.IntervalDays);
            row.LastReviewedAt = now;
            scheduled++;
        }

        return scheduled;
    }

    /// <summary>
    /// Thời điểm được thi lại, hoặc null nếu thi được ngay.
    ///
    /// Đọc từ nhật ký trạng thái chứ không có bảng riêng: một lần trượt đúng là một sự kiện
    /// trạng thái, và bảng đó vốn đã là nơi màn "vì sao bị khoá" tra cứu.
    /// </summary>
    private async Task<DateTimeOffset?> RetryAvailableAtAsync(
        Guid userId, Guid lessonId, DateTimeOffset now, CancellationToken ct)
    {
        var lastFailure = await db.LessonStateEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId
                        && e.LessonId == lessonId
                        && e.Reason == LessonStateReason.ChallengeFailed)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => (DateTimeOffset?)e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (lastFailure is null)
        {
            return null;
        }

        var retryAt = lastFailure.Value.AddHours(_policy.ChallengeCooldownHours);

        return retryAt > now ? retryAt : null;
    }

    /// <summary>
    /// Các câu dùng cho bài thi vượt.
    ///
    /// Chỉ lấy câu của những bước chấm được bằng máy. Bước Nói không có bộ chấm nên loại ra
    /// — giữ lại sẽ khiến không ai qua nổi bài thi vượt vì một trục luôn 0 điểm.
    /// Bước Viết cũng loại: nó chấm bằng luật riêng trên rubric của bước chứ không theo
    /// chỉ số lựa chọn, trộn chung vào đây sẽ chấm sai.
    /// </summary>
    private static List<ChallengeSource> SelectItems(Domain.Entities.Content.Lesson lesson)
    {
        return [.. lesson.Activities
            .Where(a => a.Kind is not (ActivityKind.Shadow or ActivityKind.Speak or ActivityKind.Write))
            .OrderBy(a => a.OrderIndex)
            .SelectMany(a => a.Items
                .OrderBy(i => i.OrderIndex)
                .Select(i => new ChallengeSource(i, a.Skill)))];
    }

    /// <summary>
    /// Một câu kèm kỹ năng của bước chứa nó.
    ///
    /// LessonItem không mang kỹ năng — nó nằm ở bước học. Ghép sẵn ở đây để chỗ chấm điểm
    /// không phải dò ngược lên bước cha mỗi lần cần quy điểm về trục.
    /// </summary>
    private record ChallengeSource(Domain.Entities.Content.LessonItem Item, SkillType Skill);

    private static string BuildMessage(
        bool passed, double score, IReadOnlyList<SkillType> below, int wrongCount, int total)
    {
        if (passed)
        {
            return wrongCount == 0
                ? $"Đúng cả {total} câu. Bài này mở luôn, bạn không phải học lại."
                : $"{score} điểm — qua bài thi vượt. {wrongCount} câu sai đã vào hàng ôn tập ngày mai.";
        }

        if (below.Count > 0)
        {
            var names = string.Join(", ", below.Select(SkillNameVi));

            return $"{score} điểm, nhưng trục {names} chưa đạt nên chưa qua được. " +
                   "Điểm tổng cao không che được một kỹ năng hổng.";
        }

        return $"{score} điểm, chưa đủ. Học bài theo cách thường sẽ nhanh hơn là thi lại.";
    }

    private static string SkillNameVi(SkillType skill) => skill switch
    {
        SkillType.Listening => "Nghe",
        SkillType.Speaking => "Nói",
        SkillType.Reading => "Đọc",
        SkillType.Writing => "Viết",
        _ => skill.ToString(),
    };

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
