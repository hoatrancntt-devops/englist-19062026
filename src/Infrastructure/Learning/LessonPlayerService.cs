using System.Text.Json;
using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Learning;

public record PlayerItem(string Code, int OrderIndex, int Difficulty, JsonElement Prompt);

public record PlayerActivity(
    Guid Id,
    string Kind,
    string Skill,
    int OrderIndex,
    int PassScore,
    JsonElement Payload,
    IReadOnlyList<PlayerItem> Items);

public record PlayerLesson(
    string Code,
    string TitleVi,
    string TitleEn,
    string Level,
    string Layer,
    string Track,
    int EstimatedMinutes,
    string ObjectiveVi,
    /// <summary>Khoá hình minh hoạ. Giao diện tra ra một component SVG nhúng sẵn.</summary>
    string? Illustration,
    JsonElement Body,
    JsonElement Explanation,
    JsonElement CommonMistakes,
    IReadOnlyList<PlayerActivity> Activities,
    string State,
    double Mastery,
    /// <summary>Rỗng khi bài đang mở. Có giá trị thì client hiện màn "vì sao bị khoá".</summary>
    string LockExplanationVi,
    /// <summary>Chỉ số bước đang làm dở, để mở lại đúng chỗ.</summary>
    int ResumeAtActivityIndex);

public record ActivitySubmission(
    Guid ActivityId,
    IReadOnlyList<ItemResponse> Responses,
    int DurationSeconds,
    /// <summary>Câu trả lời dạng chữ, dùng cho bước viết.</summary>
    IReadOnlyList<string>? TextAnswers = null);

public record LessonSubmissionResult(
    double Score,
    string State,
    Dictionary<string, double> SkillScores,
    IReadOnlyList<string> SkillsBelowThreshold,
    int ReviewItemsScheduled,
    string MessageVi);

/// <summary>
/// Phục vụ màn học: đọc bài, chấm từng bước, và chốt bài.
///
/// Nguyên tắc bất di bất dịch: đáp án không rời máy chủ. Client chỉ nhận phần đề bài
/// của item; chấm điểm và mọi quyết định trạng thái đều tính ở đây.
/// </summary>
public class LessonPlayerService(
    AppDbContext db,
    LearningPathService pathService,
    StreakService streaks,
    IOptions<LearningPolicyOptions> policyOptions,
    ILogger<LessonPlayerService> logger)
{
    private readonly LearningPolicyOptions _policy = policyOptions.Value;
    private readonly PrerequisiteEngine _engine = new(policyOptions.Value);
    private readonly ActivityGrader _grader = new();
    private readonly WritingGrader _writingGrader = new();

    /// <summary>
    /// Những loại bước còn lại theo chế độ học.
    ///
    /// Vocab và Quiz giữ trong MỌI chế độ: từ vựng là nền cho cả bốn kỹ năng, còn quiz là chỗ
    /// duy nhất chấm được bằng máy ở bài không có phần viết. Bỏ chúng đi thì chế độ "chỉ nói"
    /// còn đúng hai bước và bài mất hết chỗ bám.
    /// </summary>
    private static HashSet<ActivityKind> KindsFor(StudyMode mode) => mode switch
    {
        StudyMode.ListeningOnly => [ActivityKind.Listen, ActivityKind.Vocab, ActivityKind.Quiz],
        StudyMode.SpeakingOnly => [ActivityKind.Shadow, ActivityKind.Speak, ActivityKind.Vocab, ActivityKind.Quiz],
        StudyMode.ReadingOnly => [ActivityKind.Read, ActivityKind.Vocab, ActivityKind.Quiz],
        StudyMode.WritingOnly => [ActivityKind.Write, ActivityKind.Vocab, ActivityKind.Quiz],
        _ => [.. Enum.GetValues<ActivityKind>()],
    };

    public async Task<PlayerLesson?> GetLessonAsync(
        Guid userId, string code, DateTimeOffset now, CancellationToken ct = default)
    {
        var lesson = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Activities.OrderBy(a => a.OrderIndex))
                .ThenInclude(a => a.Items.OrderBy(i => i.OrderIndex))
            .FirstOrDefaultAsync(l => l.Code == code && l.Status == ContentStatus.Published, ct);

        if (lesson is null)
        {
            return null;
        }

        // Trạng thái lấy từ lộ trình, không tự tính lại: hai chỗ tính riêng là hai chỗ
        // sẽ lệch nhau sau vài lần sửa.
        var roadmap = await pathService.GetRoadmapAsync(userId, now, ct);
        var card = roadmap.Lessons.FirstOrDefault(c => c.Code == code);

        var draft = await db.LessonAttempts
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.LessonId == lesson.Id && a.SubmittedAt == null)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(ct);

        // Chế độ học lọc bước ngay ở đây.
        //
        // Trước đó StudyMode chỉ được HIỂN THỊ trên bảng điều khiển chứ không lọc gì: chọn
        // "chỉ nghe" vẫn phải đi qua đủ bảy bước, tức là lựa chọn đó không có tác dụng nào.
        //
        // Lọc ở đây an toàn cho phần tính mastery: engine đã bỏ qua kỹ năng không có điểm
        // (xem ComputeMasteryRaw và SkillsBelowThreshold), nên học một kỹ năng vẫn qua được bài.
        var profile = await db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var kept = KindsFor(profile?.StudyMode ?? StudyMode.Mixed);

        var activities = lesson.Activities
            .Where(a => kept.Contains(a.Kind))
            .OrderBy(a => a.OrderIndex)
            .Select(a => new PlayerActivity(
                a.Id,
                a.Kind.ToString(),
                a.Skill.ToString(),
                a.OrderIndex,
                a.PassScore,
                Parse(a.PayloadJson),
                [.. a.Items
                    .OrderBy(i => i.OrderIndex)
                    // CHỈ prompt. AnswerJson không bao giờ được đưa vào DTO này.
                    .Select(i => new PlayerItem(i.Code, i.OrderIndex, i.Difficulty, Parse(i.PromptJson)))]))
            .ToList();

        return new PlayerLesson(
            lesson.Code,
            lesson.TitleVi,
            lesson.TitleEn,
            lesson.Level.ToString(),
            lesson.Layer.ToString(),
            lesson.Track.ToString(),
            lesson.EstimatedMinutes,
            lesson.ObjectiveVi,
            lesson.Illustration,
            Parse(lesson.BodyJson),
            Parse(lesson.ExplanationJson),
            Parse(lesson.CommonMistakesJson),
            activities,
            card?.State ?? nameof(LessonState.Locked),
            card?.Mastery ?? 0,
            card?.LockExplanationVi ?? string.Empty,
            draft?.CurrentActivityIndex ?? 0);
    }

    /// <summary>Chấm một bước và ghi lại kết quả. Trả về điểm để client hiện ngay.</summary>
    public async Task<ActivityGrade?> SubmitActivityAsync(
        Guid userId, string lessonCode, ActivitySubmission submission, DateTimeOffset now, CancellationToken ct = default)
    {
        var lesson = await db.Lessons
            .FirstOrDefaultAsync(l => l.Code == lessonCode && l.Status == ContentStatus.Published, ct);

        if (lesson is null)
        {
            return null;
        }

        var activity = await db.LessonActivities
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == submission.ActivityId && a.LessonId == lesson.Id, ct);

        if (activity is null)
        {
            return null;
        }

        // Chặn mở bài bằng cách gọi thẳng API: engine là nguồn quyết định duy nhất,
        // và nó phải được hỏi ở đây chứ không chỉ ở màn hiển thị.
        var roadmap = await pathService.GetRoadmapAsync(userId, now, ct);
        var card = roadmap.Lessons.FirstOrDefault(c => c.Code == lessonCode);

        if (card is null || card.State == nameof(LessonState.Locked))
        {
            logger.LogWarning("Từ chối chấm bài {Code} cho {UserId}: bài đang khoá", lessonCode, userId);
            return null;
        }

        var grade = GradeActivity(activity, submission);

        var attempt = await GetOrCreateAttemptAsync(userId, lesson.Id, now, ct);
        attempt.CurrentActivityIndex = Math.Max(attempt.CurrentActivityIndex, activity.OrderIndex + 1);

        // Ghi bản ghi cho MỌI bước đã làm, kể cả bước chưa chấm được — nhưng đánh dấu
        // Graded để không chỗ nào nhầm điểm 0 thành làm sai. Chuỗi ngày cần biết học viên
        // có chạm kỹ năng Nói hay không, mà điều đó không suy ra được từ bảng điểm.
        {
            db.ActivityAttempts.Add(new ActivityAttempt
            {
                UserId = userId,
                LessonAttemptId = attempt.Id,
                ActivityId = activity.Id,
                Kind = activity.Kind,
                Skill = activity.Skill,
                Graded = grade.Graded,
                Score = grade.Graded ? grade.Score : 0,
                Passed = grade.Graded && grade.Passed,
                DurationSeconds = Math.Clamp(submission.DurationSeconds, 0, 3600),
                ResultJson = JsonSerializer.Serialize(new
                {
                    grade.Score,
                    grade.Passed,
                    Items = grade.Items,
                }),
            });
        }

        await db.SaveChangesAsync(ct);

        // Tính chuỗi ngay sau mỗi bước, không đợi lúc chốt bài: học viên làm ba bước rồi
        // nghỉ vẫn phải được cộng đủ số phút của ba bước đó vào ngày hôm nay.
        await streaks.RecordStudyAsync(userId, now, ct);

        return grade;
    }

    /// <summary>
    /// Chốt bài: gộp điểm các bước thành điểm từng trục, tính mastery, cập nhật trạng thái,
    /// ghi nhật ký lý do, và xếp lịch ôn tập cho các câu vừa làm.
    /// </summary>
    public async Task<LessonSubmissionResult?> SubmitLessonAsync(
        Guid userId, string lessonCode, DateTimeOffset now, CancellationToken ct = default)
    {
        var lesson = await db.Lessons
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Code == lessonCode && l.Status == ContentStatus.Published, ct);

        if (lesson is null)
        {
            return null;
        }

        var attempt = await db.LessonAttempts
            .Where(a => a.UserId == userId && a.LessonId == lesson.Id && a.SubmittedAt == null)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (attempt is null)
        {
            return null;
        }

        // CHỈ bản ghi đã chấm. Bước Nói nay cũng có bản ghi (để chuỗi ngày biết học viên
        // đã chạm kỹ năng đó), nhưng Score của nó là 0 vì chưa chấm được, không phải vì làm sai.
        var activityAttempts = await db.ActivityAttempts
            .Where(a => a.LessonAttemptId == attempt.Id && a.Graded)
            .ToListAsync(ct);

        if (activityAttempts.Count == 0)
        {
            return null;
        }

        // Mỗi bước lấy lần làm tốt nhất: học viên làm lại một bước để sửa lỗi
        // không được bị phạt vì lần làm hỏng trước đó.
        var bestPerActivity = activityAttempts
            .GroupBy(a => a.ActivityId)
            .Select(g => g.OrderByDescending(a => a.Score).First())
            .ToList();

        var skillScores = _grader.AggregateSkillScores([.. bestPerActivity.Select(a => (a.Skill, a.Score))]);

        // Chỉ tính trọng số cho kỹ năng THỰC SỰ chấm được. Giữ nguyên trọng số của kỹ năng
        // chưa có bộ chấm (hiện là Nói) sẽ kéo mastery xuống mãi mãi và không ai qua nổi bài nào.
        var assessableWeights = lesson.MasteryWeights
            .Where(w => skillScores.ContainsKey(w.Key))
            .ToDictionary(w => w.Key, w => w.Value);

        var masteryRaw = _engine.ComputeMasteryRaw(skillScores, assessableWeights);

        // Engine tự bỏ qua kỹ năng chưa có điểm, nên truyền thẳng danh sách đầy đủ.
        var below = _engine.SkillsBelowThreshold(skillScores, lesson.SupportedSkills);

        var unassessed = lesson.SupportedSkills.Where(s => !skillScores.ContainsKey(s)).ToList();

        var mastery = await GetOrCreateMasteryAsync(userId, lesson.Id, ct);
        var previousState = mastery.State;

        mastery.MasteryRaw = masteryRaw;
        mastery.MasteryEffective = masteryRaw;
        mastery.SkillScores = skillScores;
        mastery.AttemptsCount++;

        // Đã học thật thì điểm này không còn là điểm thi vượt nữa, nên bỏ cờ.
        // Quên dòng này thì người từng thi vượt rồi quay lại học tử tế vẫn bị chặn vĩnh viễn:
        // cờ chỉ được bật, không bao giờ tắt, và họ không có cách nào tự gỡ.
        mastery.UnlockedByChallenge = false;
        mastery.LastActivityAt = now;
        mastery.FirstStartedAt ??= attempt.StartedAt;
        mastery.TimeSpentSeconds += bestPerActivity.Sum(a => a.DurationSeconds);

        var reachedMastery = masteryRaw >= _policy.MasteryThreshold && below.Count == 0;

        if (reachedMastery)
        {
            mastery.State = LessonState.Mastered;
            mastery.MasteredAt = now;
        }
        else
        {
            mastery.State = LessonState.InProgress;
        }

        attempt.SubmittedAt = now;
        attempt.Score = masteryRaw;

        db.LessonStateEvents.Add(new LessonStateEvent
        {
            UserId = userId,
            LessonId = lesson.Id,
            FromState = previousState,
            ToState = mastery.State,
            Reason = reachedMastery ? LessonStateReason.MasteryReached : LessonStateReason.SkillBelowThreshold,
            DetailJson = JsonSerializer.Serialize(new
            {
                masteryRaw,
                skillScores = skillScores.ToDictionary(k => k.Key.ToString(), v => v.Value),
                below = below.Select(s => s.ToString()),
            }),
        });

        var scheduled = await ScheduleReviewsAsync(userId, attempt.Id, now, ct);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Học viên {UserId} chốt bài {Code}: mastery {Mastery}, trạng thái {State}",
            userId, lessonCode, masteryRaw, mastery.State);

        return new LessonSubmissionResult(
            masteryRaw,
            mastery.State.ToString(),
            skillScores.ToDictionary(k => k.Key.ToString(), v => v.Value),
            [.. below.Select(s => s.ToString())],
            scheduled,
            BuildMessage(reachedMastery, masteryRaw, below, unassessed));
    }

    /// <summary>
    /// Xếp lịch ôn tập cho các câu vừa làm.
    /// Đúng thì giãn ra theo hệ số, sai thì kéo hệ số về 1.3 và hẹn lại sau một ngày.
    /// </summary>
    private async Task<int> ScheduleReviewsAsync(
        Guid userId, Guid lessonAttemptId, DateTimeOffset now, CancellationToken ct)
    {
        var attempts = await db.ActivityAttempts
            .Where(a => a.LessonAttemptId == lessonAttemptId && a.Graded)
            .ToListAsync(ct);

        var results = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var attempt in attempts)
        {
            using var doc = JsonDocument.Parse(attempt.ResultJson);

            if (!doc.RootElement.TryGetProperty("Items", out var items))
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                var code = item.GetProperty("ItemCode").GetString();
                var correct = item.GetProperty("Correct").GetBoolean();

                if (code is null)
                {
                    continue;
                }

                // Làm lại nhiều lần thì lấy kết quả tốt nhất, giống cách tính mastery.
                results[code] = results.GetValueOrDefault(code) || correct;
            }
        }

        if (results.Count == 0)
        {
            return 0;
        }

        var itemsByCode = await db.LessonItems
            .Where(i => results.Keys.Contains(i.Code))
            .ToDictionaryAsync(i => i.Code, StringComparer.Ordinal, ct);

        var existing = await db.ReviewQueue
            .Where(r => r.UserId == userId && itemsByCode.Values.Select(i => i.Id).Contains(r.ItemId))
            .ToDictionaryAsync(r => r.ItemId, ct);

        var scheduled = 0;

        foreach (var (code, correct) in results)
        {
            if (!itemsByCode.TryGetValue(code, out var item))
            {
                continue;
            }

            if (!existing.TryGetValue(item.Id, out var row))
            {
                row = new ReviewQueueItem
                {
                    UserId = userId,
                    ItemId = item.Id,
                    IntervalDays = 1,
                    Ease = 2.5,
                };

                db.ReviewQueue.Add(row);
            }

            if (correct)
            {
                row.RepetitionCount++;
                // Trần 60 ngày: quá mốc đó thì việc ôn không còn ý nghĩa thực tế.
                row.IntervalDays = Math.Clamp((int)Math.Round(row.IntervalDays * row.Ease), 1, 60);
                row.Ease = Math.Min(3.0, row.Ease + 0.05);
            }
            else
            {
                row.LapseCount++;
                row.IntervalDays = 1;
                // Sàn 1.3 khớp ràng buộc CHECK ở tầng DB.
                row.Ease = Math.Max(1.3, row.Ease - 0.2);
            }

            row.DueAt = now.AddDays(row.IntervalDays);
            row.LastReviewedAt = now;
            scheduled++;
        }

        return scheduled;
    }

    /// <summary>
    /// Chọn bộ chấm theo loại bước. Mỗi loại đo một thứ khác nhau nên không thể dùng chung
    /// một cách chấm.
    /// </summary>
    private ActivityGrade GradeActivity(
        Domain.Entities.Content.LessonActivity activity,
        ActivitySubmission submission)
    {
        switch (activity.Kind)
        {
            case ActivityKind.Shadow:
            case ActivityKind.Speak:
                // Chấm phát âm cần dịch vụ nhận dạng giọng nói, chưa có ở giai đoạn này.
                // Trả về chưa-chấm-được thay vì bịa điểm.
                return new ActivityGrade(
                    0, false, [],
                    "Phần nói chưa chấm được tự động. Bạn vẫn nên đọc to theo mẫu — "
                    + "phần chấm phát âm sẽ bật ở bản sau.",
                    Graded: false);

            case ActivityKind.Write:
            {
                var rubric = ReadWritingRubric(activity.PayloadJson);

                if (rubric is null)
                {
                    return new ActivityGrade(0, false, [], "Bài viết thiếu dữ liệu chấm.", Graded: false);
                }

                var writing = _writingGrader.Grade(
                    rubric, submission.TextAnswers ?? [], activity.PassScore);

                return new ActivityGrade(
                    writing.Score, writing.Passed, [], writing.FeedbackVi, SampleEn: writing.SampleEn);
            }

            default:
            {
                var answerKey = activity.Items.ToDictionary(i => i.Code, i => i.AnswerJson, StringComparer.Ordinal);
                return _grader.GradeMultipleChoice(submission.Responses, answerKey, activity.PassScore);
            }
        }
    }

    private static WritingRubric? ReadWritingRubric(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var kind = root.TryGetProperty("Kind", out var kindValue)
                && Enum.TryParse<WritingTaskKind>(kindValue.GetString(), out var parsed)
                    ? parsed
                    : WritingTaskKind.FillBlank;

            return new WritingRubric(
                kind,
                ReadNestedList(root, "Blanks"),
                ReadList(root, "CorrectOrder"),
                ReadList(root, "RequiredPoints"),
                root.TryGetProperty("SampleEn", out var sample) ? sample.GetString() ?? string.Empty : string.Empty);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string> ReadList(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Select(e => e.GetString() ?? string.Empty)]
            : [];

    private static List<IReadOnlyList<string>> ReadNestedList(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Select(inner =>
                (IReadOnlyList<string>)inner.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList())]
            : [];

    private async Task<LessonAttempt> GetOrCreateAttemptAsync(
        Guid userId, Guid lessonId, DateTimeOffset now, CancellationToken ct)
    {
        var attempt = await db.LessonAttempts
            .Where(a => a.UserId == userId && a.LessonId == lessonId && a.SubmittedAt == null)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (attempt is not null)
        {
            return attempt;
        }

        attempt = new LessonAttempt { UserId = userId, LessonId = lessonId, StartedAt = now };
        db.LessonAttempts.Add(attempt);

        return attempt;
    }

    private async Task<LessonMastery> GetOrCreateMasteryAsync(Guid userId, Guid lessonId, CancellationToken ct)
    {
        var mastery = await db.LessonMasteries
            .FirstOrDefaultAsync(m => m.UserId == userId && m.LessonId == lessonId, ct);

        if (mastery is not null)
        {
            return mastery;
        }

        mastery = new LessonMastery { UserId = userId, LessonId = lessonId, State = LessonState.InProgress };
        db.LessonMasteries.Add(mastery);

        return mastery;
    }

    private static string BuildMessage(
        bool mastered,
        double score,
        IReadOnlyList<SkillType> below,
        IReadOnlyList<SkillType> unassessed)
    {
        // Nói rõ kỹ năng nào chưa được chấm. Im lặng ở đây sẽ khiến học viên tin rằng
        // họ đã thạo cả phần nói, trong khi hệ thống chưa hề nghe họ nói câu nào.
        var caveat = unassessed.Count > 0
            ? $" Lưu ý: kỹ năng {string.Join(", ", unassessed.Select(ViName))} chưa được chấm ở bản này, "
              + "nên điểm trên chưa phản ánh phần đó."
            : string.Empty;

        if (mastered)
        {
            return $"Xong bài với {score:0.#} điểm. Bài này đã thạo.{caveat}";
        }

        if (below.Count > 0)
        {
            var names = string.Join(", ", below.Select(ViName));
            return $"Được {score:0.#} điểm, nhưng còn yếu ở kỹ năng {names}. "
                 + $"Làm lại phần đó để chốt bài.{caveat}";
        }

        return $"Được {score:0.#} điểm, chưa đủ ngưỡng. Xem lại phần giải thích rồi làm lại.{caveat}";
    }

    private static string ViName(SkillType skill) => skill switch
    {
        SkillType.Listening => "Nghe",
        SkillType.Speaking => "Nói",
        SkillType.Reading => "Đọc",
        SkillType.Writing => "Viết",
        _ => skill.ToString(),
    };

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
