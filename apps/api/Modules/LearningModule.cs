using System.Security.Claims;
using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Learning;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.Api.Modules;

public record StreakSummary(
    int Current,
    int Longest,
    int FreezeTokens,
    bool StudiedToday,
    int MinutesToday,
    int MinutesTarget,
    string[] SkillsTouchedToday,
    /// <summary>Câu nói thẳng vì sao hôm nay chưa tính vào chuỗi. Ô đếm im lặng gây hiểu nhầm.</summary>
    string ReasonVi);

/// <summary>Một câu ôn được nộp. Client gửi lựa chọn, đáp án ở lại máy chủ.</summary>
public record ReviewAnswerSubmission(string ItemCode, int ChosenIndex);

/// <summary>Bài thi vượt được nộp trọn gói: không chấm từng câu để không lộ đáp án giữa chừng.</summary>
public record ChallengeSubmission(IReadOnlyList<ItemResponse> Responses);

public record ProgressSummary(
    int LessonsMastered,
    int LessonsTotal,
    int LessonsInProgress,
    int MinutesStudiedLast7Days,
    int? EstimatedDaysRemaining);

public record NextLessonSummary(
    string Code,
    string TitleVi,
    string Track,
    string Layer,
    string Level,
    int EstimatedMinutes,
    string ReasonVi,
    string[] SupportedSkills,
    string? Illustration);

public record MilestoneSummary(string Key, string LabelVi, bool Achieved, double ProgressPercent, string RequirementVi);

public record DashboardResponse(
    string DisplayName,
    string StudyMode,
    string CurrentLevel,
    string CurrentLayer,
    bool PlacementCompleted,
    StreakSummary Streak,
    Dictionary<string, double> SkillScores,
    ProgressSummary Progress,
    int ReviewDueCount,
    NextLessonSummary? NextLesson,
    MilestoneSummary[] Milestones);

/// <summary>
/// Đọc dữ liệu cho bảng điều khiển học viên.
///
/// Part 1 dựng phần đọc: mọi con số đều lấy thật từ DB, không có giá trị giả.
/// Học viên mới thì các số bằng 0 — đó là dữ liệu đúng, không phải chỗ trống cần lấp.
/// Thuật toán chọn bài kế và tính mastery thuộc Part 2.
/// </summary>
public static class LearningModule
{
    public static IEndpointRouteBuilder MapLearningModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/learning").WithTags("Learning");

        group.MapGet("/dashboard", GetDashboard)
            .WithSummary("Toàn bộ dữ liệu bảng điều khiển học viên trong một request");

        group.MapGet("/roadmap", GetRoadmap)
            .WithSummary("Toàn bộ lộ trình kèm trạng thái và lý do khoá từng bài");

        group.MapGet("/lessons/{code}", GetLesson)
            .WithSummary("Nội dung một bài để hiển thị trong màn học. Không bao giờ kèm đáp án.");

        group.MapPost("/lessons/{code}/activities", SubmitActivity)
            .WithSummary("Nộp một bước học. Chấm tại máy chủ và trả kết quả ngay.");

        group.MapPost("/lessons/{code}/submit", SubmitLesson)
            .WithSummary("Chốt bài: tính mastery, cập nhật trạng thái, xếp lịch ôn tập.");

        group.MapGet("/review", GetReviewSession)
            .WithSummary("Các câu tới hạn ôn, quá hạn lâu nhất trước. Không bao giờ kèm đáp án.");

        group.MapPost("/review/answer", SubmitReviewAnswer)
            .WithSummary("Chấm một câu ôn và xếp lịch lại ngay. Không đụng vào mastery của bài.");

        group.MapGet("/lessons/{code}/challenge", GetChallenge)
            .WithSummary("Đề thi vượt của một bài, hoặc lý do bài đó chưa thi vượt được.");

        group.MapPost("/lessons/{code}/challenge", SubmitChallenge)
            .WithSummary("Nộp bài thi vượt. Qua thì mở bài luôn, trượt thì phải chờ mới thi lại.");

        return app;
    }

    private static async Task<IResult> GetChallenge(
        string code,
        ClaimsPrincipal principal,
        ChallengeService challenge,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var offer = await challenge.GetOfferAsync(userId, code, DateTimeOffset.UtcNow, ct);

        return offer is null
            ? Results.NotFound(new { error = "lesson_not_found", message = "Không có bài nào mang mã này." })
            : Results.Ok(offer);
    }

    private static async Task<IResult> SubmitChallenge(
        string code,
        [FromBody] ChallengeSubmission submission,
        ClaimsPrincipal principal,
        ChallengeService challenge,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await challenge.SubmitAsync(
            userId, code, submission.Responses, DateTimeOffset.UtcNow, ct);

        return result is null
            ? Results.NotFound(new { error = "lesson_not_found", message = "Không có bài nào mang mã này." })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetReviewSession(
        ClaimsPrincipal principal,
        ReviewService review,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await review.GetSessionAsync(userId, DateTimeOffset.UtcNow, ct));
    }

    private static async Task<IResult> SubmitReviewAnswer(
        [FromBody] ReviewAnswerSubmission submission,
        ClaimsPrincipal principal,
        ReviewService review,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await review.SubmitAnswerAsync(
            userId, submission.ItemCode, submission.ChosenIndex, DateTimeOffset.UtcNow, ct);

        return result is null
            ? Results.NotFound(new { error = "review_item_not_found", message = "Câu này không nằm trong hàng đợi ôn của bạn." })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetRoadmap(
        ClaimsPrincipal principal,
        LearningPathService pathService,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var roadmap = await pathService.GetRoadmapAsync(userId, DateTimeOffset.UtcNow, ct);
        return Results.Ok(roadmap);
    }

    private static async Task<IResult> GetLesson(
        string code,
        ClaimsPrincipal principal,
        LessonPlayerService player,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var lesson = await player.GetLessonAsync(userId, code, DateTimeOffset.UtcNow, ct);

        return lesson is null
            ? Results.NotFound(new { error = "lesson_not_found", message = "Không tìm thấy bài học này." })
            : Results.Ok(lesson);
    }

    private static async Task<IResult> SubmitActivity(
        string code,
        [FromBody] ActivitySubmission submission,
        ClaimsPrincipal principal,
        LessonPlayerService player,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var grade = await player.SubmitActivityAsync(userId, code, submission, DateTimeOffset.UtcNow, ct);

        return grade is null
            ? Results.BadRequest(new
            {
                error = "activity_not_submittable",
                message = "Bài hoặc bước học không hợp lệ, hoặc bài đang bị khoá.",
            })
            : Results.Ok(grade);
    }

    private static async Task<IResult> SubmitLesson(
        string code,
        ClaimsPrincipal principal,
        LessonPlayerService player,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await player.SubmitLessonAsync(userId, code, DateTimeOffset.UtcNow, ct);

        return result is null
            ? Results.BadRequest(new
            {
                error = "nothing_to_submit",
                message = "Chưa có bước nào được làm trong bài này.",
            })
            : Results.Ok(result);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;

        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private static async Task<IResult> GetDashboard(
        ClaimsPrincipal principal,
        AppDbContext db,
        LearningPathService pathService,
        StreakService streaks,
        CancellationToken ct)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var now = DateTimeOffset.UtcNow;

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile is null)
        {
            return Results.NotFound(new { error = "profile_missing", message = "Không tìm thấy hồ sơ học." });
        }

        var placementCompleted = await db.PlacementAttempts
            .AsNoTracking()
            .AnyAsync(a => a.UserId == userId && a.Status == PlacementAttemptStatus.Submitted, ct);

        var streakRow = await db.Streaks.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);

        // Ngày học tính theo lịch địa phương của học viên, không theo UTC —
        // người học lúc 11 giờ đêm ở Việt Nam vẫn phải được tính là hôm nay.
        //
        // Dùng múi giờ trong hồ sơ chứ không cộng cứng 7 tiếng: cộng cứng thì đúng với
        // người ở Việt Nam và sai với mọi người khác.
        var today = await streaks.TodayAsync(userId, now, ct);
        var studiedToday = streakRow?.LastStudyDateLocal == today.DateLocal;

        var masteries = await db.LessonMasteries
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { m.State, m.SkillScores, m.TimeSpentSeconds, m.LastActivityAt })
            .ToListAsync(ct);

        // Lộ trình tính một lần rồi lấy cả tổng số bài lẫn bài kế từ đó — tránh
        // đếm bài ở một chỗ và chọn bài kế ở chỗ khác rồi hai con số lệch nhau.
        var roadmap = await pathService.GetRoadmapAsync(userId, now, ct);

        var sevenDaysAgo = now.AddDays(-7);
        var secondsLast7Days = masteries
            .Where(m => m.LastActivityAt >= sevenDaysAgo)
            .Sum(m => m.TimeSpentSeconds);

        var skillScores = AverageSkillScores(masteries.Select(m => m.SkillScores));

        var reviewDue = await db.ReviewQueue
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId && r.DueAt <= now, ct);

        // Lấy từ lộ trình chứ không đếm thẳng lesson_mastery: engine có thể hạ một bài
        // từ Mastered xuống NeedsReview do suy giảm theo thời gian, và con số hiển thị
        // phải khớp với thứ học viên nhìn thấy trên lộ trình.
        var mastered = roadmap.Mastered;
        var inProgress = roadmap.InProgress;
        var lessonsTotal = roadmap.TotalPublished;

        var response = new DashboardResponse(
            DisplayName: principal.FindFirstValue(ClaimTypes.Name) ?? "bạn",
            StudyMode: profile.StudyMode.ToString(),
            CurrentLevel: profile.CurrentLevel.ToString(),
            CurrentLayer: profile.CurrentLayer.ToString(),
            PlacementCompleted: placementCompleted,
            Streak: new StreakSummary(
                streakRow?.CurrentStreak ?? 0,
                streakRow?.LongestStreak ?? 0,
                streakRow?.FreezeTokens ?? 0,
                studiedToday,
                today.MinutesStudied,
                today.MinutesTarget,
                [.. today.SkillsTouched.Select(s => s.ToString())],
                today.ReasonVi),
            SkillScores: skillScores,
            Progress: new ProgressSummary(
                mastered,
                lessonsTotal,
                inProgress,
                secondsLast7Days / 60,
                EstimateDaysRemaining(mastered, lessonsTotal, secondsLast7Days, profile.DailyMinutesTarget)),
            ReviewDueCount: reviewDue,
            NextLesson: roadmap.Next is { } next
                ? new NextLessonSummary(
                    next.Card.Code,
                    next.Card.TitleVi,
                    next.Card.Track,
                    next.Card.Layer,
                    next.Card.Level,
                    next.Card.EstimatedMinutes,
                    next.ReasonVi,
                    [.. next.Card.SupportedSkills],
                    next.Card.Illustration)
                : null,
            Milestones: BuildMilestones(mastered));

        return Results.Ok(response);
    }

    private static Dictionary<string, double> AverageSkillScores(IEnumerable<Dictionary<SkillType, double>> rows)
    {
        var totals = new Dictionary<SkillType, (double Sum, int Count)>();

        foreach (var row in rows)
        {
            foreach (var (skill, score) in row)
            {
                var current = totals.GetValueOrDefault(skill);
                totals[skill] = (current.Sum + score, current.Count + 1);
            }
        }

        // Luôn trả đủ bốn khoá: giao diện vẽ bốn trục và không nên phải xử lý khoá thiếu.
        return Enum.GetValues<SkillType>().ToDictionary(
            skill => skill.ToString(),
            skill => totals.TryGetValue(skill, out var t) && t.Count > 0
                ? Math.Round(t.Sum / t.Count, 1)
                : 0d);
    }

    /// <summary>
    /// Ước lượng từ nhịp học thật của bảy ngày qua, không từ hằng số.
    /// Chưa học buổi nào thì trả null — thà không nói gì còn hơn nói một con số bịa.
    /// </summary>
    private static int? EstimateDaysRemaining(int mastered, int total, int secondsLast7Days, int dailyMinutesTarget)
    {
        var remaining = total - mastered;
        if (remaining <= 0 || secondsLast7Days <= 0)
        {
            return null;
        }

        var minutesPerDay = secondsLast7Days / 60.0 / 7.0;
        if (minutesPerDay < 1)
        {
            return null;
        }

        // 11 phút là thời lượng trung bình một bài; trần bài là 12 phút.
        const double MinutesPerLesson = 11.0;
        var pace = Math.Max(minutesPerDay, dailyMinutesTarget * 0.3);

        return (int)Math.Ceiling(remaining * MinutesPerLesson / pace);
    }

    /// <summary>
    /// Năm mốc nghề nghiệp. Part 1 tính theo số bài đã thạo; Part 2 sẽ đổi sang
    /// điều kiện thật của từng mốc (điểm nói, bài viết đạt, roleplay không dùng gợi ý).
    /// </summary>
    private static MilestoneSummary[] BuildMilestones(int mastered)
    {
        (CareerMilestone Key, string Label, int Needed, string Requirement)[] definitions =
        [
            (CareerMilestone.JoinStandupConfidently, "Tự tin dự standup", 8,
                "Nói trọn ba phần standup, điểm truyền đạt từ 75"),
            (CareerMilestone.WriteIncidentReport, "Tự viết incident report", 14,
                "Bài viết đủ 6 trường bắt buộc, đạt từ 80"),
            (CareerMilestone.CallVendorSupport, "Tự gọi vendor support", 20,
                "Hoàn thành roleplay vendor không dùng gợi ý"),
            (CareerMilestone.PresentCloudSolution, "Tự trình bày cloud solution", 26,
                "Nêu đánh đổi theo 2 trụ cột, điểm nói từ 75"),
            (CareerMilestone.ProposeAiUseCase, "Tự đề xuất AI use case", 32,
                "Proposal đủ 4 mục của NIST AI RMF, đạt từ 80"),
        ];

        return definitions
            .Select(d => new MilestoneSummary(
                d.Key.ToString(),
                d.Label,
                mastered >= d.Needed,
                Math.Min(100, Math.Round(mastered * 100.0 / d.Needed, 1)),
                d.Requirement))
            .ToArray();
    }
}
