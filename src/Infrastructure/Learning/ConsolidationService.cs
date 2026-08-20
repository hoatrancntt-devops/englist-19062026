using System.Text.Json;
using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Learning;

/// <summary>Một câu trong bài tổng hợp. Chỉ đề, không đáp án.</summary>
public record ConsolidationItem(
    string Code,
    string LessonCode,
    string Skill,
    int Difficulty,
    JsonElement Prompt);

/// <summary>
/// Nhóm bài tổng hợp đang chờ.
///
/// <c>Pending</c> false nghĩa là chưa tới lượt: học viên chưa thạo đủ số bài cho nhóm kế tiếp.
/// </summary>
public record ConsolidationOffer(
    bool Pending,
    int GroupIndex,
    IReadOnlyList<string> LessonCodes,
    IReadOnlyList<string> LessonTitles,
    int PassThreshold,
    IReadOnlyList<ConsolidationItem> Items,
    string MessageVi);

public record ConsolidationResult(
    bool Passed,
    double Score,
    int PassThreshold,
    int CorrectCount,
    int TotalCount,
    IReadOnlyList<string> WrongItemCodes,
    string MessageVi);

/// <summary>
/// Bài tổng hợp: cứ ba bài thạo thì phải ôn lại đúng ba bài đó mới học tiếp.
///
/// Đề sinh lúc chạy chứ không soạn sẵn, vì nhóm gom theo thứ tự học viên thạo bài — người
/// chọn "tất cả lĩnh vực" có bộ ba trộn nhiều nhánh, và không bộ nội dung soạn tay nào phủ
/// được mọi tổ hợp đó.
///
/// Câu lấy theo thứ tự cố định (bước rồi tới câu), không bốc ngẫu nhiên: tải lại trang phải
/// ra đúng đề cũ, nếu không học viên bấm F5 là đổi được bộ câu dễ hơn.
/// </summary>
public class ConsolidationService(
    AppDbContext db,
    IOptions<LearningPolicyOptions> policyOptions,
    ILogger<ConsolidationService> logger)
{
    private readonly LearningPolicyOptions _policy = policyOptions.Value;
    private readonly ActivityGrader _grader = new();

    /// <summary>
    /// Những dạng bước không tự chấm được thì không đưa vào bài tổng hợp.
    /// Cùng danh sách loại trừ với bài thi vượt.
    /// </summary>
    private static bool IsAutoGradable(ActivityKind kind) =>
        kind is not (ActivityKind.Shadow or ActivityKind.Speak or ActivityKind.Write);

    public async Task<ConsolidationOffer> GetOfferAsync(
        Guid userId, CancellationToken ct = default)
    {
        var group = await FindPendingGroupAsync(userId, ct);

        if (group is null)
        {
            return new ConsolidationOffer(
                false, 0, [], [], _policy.ConsolidationPassThreshold, [],
                $"Chưa tới lượt ôn tổng hợp. Cứ thạo {_policy.ConsolidationGroupSize} bài thì mở một bài ôn lại đúng {_policy.ConsolidationGroupSize} bài đó.");
        }

        var items = BuildItems(group.Lessons);

        if (items.Count == 0)
        {
            // Ba bài toàn bước nói và viết thì không sinh được câu nào để chấm bằng máy.
            // Không chặn học viên vì chuyện của nội dung: coi như nhóm này không có cổng.
            logger.LogWarning(
                "Nhóm tổng hợp {Group} của {UserId} không có câu nào chấm được, bỏ qua cổng",
                group.Index, userId);

            return new ConsolidationOffer(
                false, group.Index, [], [], _policy.ConsolidationPassThreshold, [],
                "Ba bài vừa rồi không có câu nào chấm tự động được, nên không có bài tổng hợp.");
        }

        return new ConsolidationOffer(
            true,
            group.Index,
            [.. group.Lessons.Select(l => l.Code)],
            [.. group.Lessons.Select(l => l.TitleVi)],
            _policy.ConsolidationPassThreshold,
            [.. items.Select(s => new ConsolidationItem(
                s.Item.Code,
                s.LessonCode,
                s.Skill.ToString(),
                s.Item.Difficulty,
                Parse(s.Item.PromptJson)))],
            $"Ôn lại ba bài vừa học. Đạt {_policy.ConsolidationPassThreshold} điểm là mở tiếp lộ trình.");
    }

    public async Task<ConsolidationResult?> SubmitAsync(
        Guid userId,
        IReadOnlyList<ItemResponse> responses,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var group = await FindPendingGroupAsync(userId, ct);

        if (group is null)
        {
            return null;
        }

        var items = BuildItems(group.Lessons);

        if (items.Count == 0)
        {
            return null;
        }

        var answerKey = items.ToDictionary(s => s.Item.Code, s => s.Item.AnswerJson, StringComparer.Ordinal);
        var grade = _grader.GradeMultipleChoice(responses, answerKey, _policy.ConsolidationPassThreshold);

        var passed = grade.Score >= _policy.ConsolidationPassThreshold;
        var wrong = grade.Items.Where(g => !g.Correct).Select(g => g.ItemCode).ToList();

        if (passed)
        {
            db.ConsolidationPasses.Add(new ConsolidationPass
            {
                UserId = userId,
                GroupIndex = group.Index,
                Score = grade.Score,
                PassedAt = now,
                LessonCodesJson = JsonSerializer.Serialize(group.Lessons.Select(l => l.Code)),
            });

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Học viên {UserId} qua bài tổng hợp nhóm {Group} với {Score} điểm",
                userId, group.Index, grade.Score);
        }

        return new ConsolidationResult(
            passed,
            grade.Score,
            _policy.ConsolidationPassThreshold,
            grade.Items.Count(g => g.Correct),
            grade.Items.Count,
            wrong,
            passed
                ? $"{grade.Score} điểm — qua bài tổng hợp. Lộ trình mở tiếp."
                : $"{grade.Score} điểm, chưa đủ {_policy.ConsolidationPassThreshold}. Xem lại ba bài rồi làm lại, không có thời gian chờ.");
    }

    /// <summary>
    /// Nhóm đang chờ, hoặc null khi không có.
    ///
    /// Nhóm xếp theo mốc thạo LẦN ĐẦU, nên học lại một bài cũ không xáo lại thứ tự nhóm.
    /// </summary>
    private async Task<PendingGroup?> FindPendingGroupAsync(Guid userId, CancellationToken ct)
    {
        var size = Math.Max(1, _policy.ConsolidationGroupSize);

        var masteredLessonIds = await db.LessonMasteries
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.MasteredAt != null && !m.UnlockedByChallenge)
            .OrderBy(m => m.MasteredAt)
            .ThenBy(m => m.LessonId)
            .Select(m => m.LessonId)
            .ToListAsync(ct);

        var completeGroups = masteredLessonIds.Count / size;

        if (completeGroups == 0)
        {
            return null;
        }

        var passedGroups = await db.ConsolidationPasses
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.GroupIndex)
            .ToHashSetAsync(ct);

        var pendingIndex = Enumerable.Range(1, completeGroups).FirstOrDefault(g => !passedGroups.Contains(g));

        if (pendingIndex == 0)
        {
            return null;
        }

        var idsInGroup = masteredLessonIds
            .Skip((pendingIndex - 1) * size)
            .Take(size)
            .ToList();

        var lessons = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Activities)
                .ThenInclude(a => a.Items)
            .Where(l => idsInGroup.Contains(l.Id))
            .ToListAsync(ct);

        // Giữ đúng thứ tự thạo, không để thứ tự của câu truy vấn quyết định.
        var ordered = idsInGroup
            .Select(id => lessons.FirstOrDefault(l => l.Id == id))
            .OfType<Lesson>()
            .ToList();

        return ordered.Count == 0 ? null : new PendingGroup(pendingIndex, ordered);
    }

    private List<ItemSource> BuildItems(IReadOnlyList<Lesson> lessons)
    {
        var perLesson = Math.Max(1, _policy.ConsolidationItemsPerLesson);
        var picked = new List<ItemSource>();

        foreach (var lesson in lessons)
        {
            picked.AddRange(lesson.Activities
                .Where(a => IsAutoGradable(a.Kind))
                .OrderBy(a => a.OrderIndex)
                .SelectMany(a => a.Items
                    .OrderBy(i => i.OrderIndex)
                    .Select(i => new ItemSource(i, a.Skill, lesson.Code)))
                .Take(perLesson));
        }

        return picked;
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private record PendingGroup(int Index, IReadOnlyList<Lesson> Lessons);

    private record ItemSource(LessonItem Item, SkillType Skill, string LessonCode);
}
