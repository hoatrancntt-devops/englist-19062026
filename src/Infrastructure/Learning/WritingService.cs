using System.Text.Json;
using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Learning;

public record WritingSetSummary(
    string Code,
    string TitleVi,
    string ContextVi,
    string Track,
    string Level,
    int TaskCount,
    int PassedCount);

/// <summary>Đề của một bài. Không có đáp án — chúng nằm ở cột rubric và không rời máy chủ.</summary>
public record WritingTaskView(
    string Code,
    string Kind,
    int PassScore,
    string PromptVi,
    string? PromptEn,
    string? HintVi,
    /// <summary>Dạng Reorder: các mảnh theo thứ tự đã xáo sẵn trong file nội dung.</summary>
    IReadOnlyList<string> Fragments,
    /// <summary>Dạng FillBlank: số ô cần nhập. Chỉ là con số, không kèm đáp án.</summary>
    int BlankCount,
    /// <summary>Điểm cao nhất từng đạt, không phải điểm lần nộp gần nhất.</summary>
    double? BestScore,
    /// <summary>Đã từng đạt hay chưa. Làm lại bị điểm thấp không xoá kết quả đã đạt.</summary>
    bool Passed);

public record WritingSetDetail(
    string Code,
    string TitleVi,
    string ContextVi,
    string Track,
    string Level,
    IReadOnlyList<WritingTaskView> Tasks);

/// <summary>Kết quả một lần nộp. Câu mẫu chỉ xuất hiện ở đây, tức là sau khi đã nộp.</summary>
public record WritingSubmitResult(double Score, bool Passed, string FeedbackVi, string SampleEn);

/// <summary>
/// Bộ bài luyện viết, chấm bằng luật tại máy chủ.
///
/// Dùng lại nguyên <see cref="WritingGrader"/> của bước viết trong bài học: cùng một bài viết
/// phải được chấm y hệt nhau dù nó nằm trong bài học hay trong bộ drill. Viết bộ chấm thứ hai
/// là cách chắc chắn để hai chỗ trôi dần khỏi nhau.
/// </summary>
public class WritingService(AppDbContext db, ILogger<WritingService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly WritingGrader _grader = new();

    public async Task<IReadOnlyList<WritingSetSummary>> ListAsync(
        Guid userId, CancellationToken ct = default)
    {
        var sets = await db.WritingSets
            .AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new
            {
                s.Id,
                s.Code,
                s.TitleVi,
                s.ContextVi,
                s.Track,
                s.Level,
                TaskCount = s.Tasks.Count,
            })
            .ToListAsync(ct);

        if (sets.Count == 0)
        {
            return [];
        }

        // Đếm theo bài chứ không theo lần nộp: làm lại một bài mười lần vẫn là một bài đạt.
        var passed = await db.WritingAttempts
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Passed)
            .Select(a => new { a.TaskId, a.Task!.SetId })
            .Distinct()
            .ToListAsync(ct);

        var passedBySet = passed
            .GroupBy(x => x.SetId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TaskId).Distinct().Count());

        return [.. sets.Select(s => new WritingSetSummary(
            s.Code,
            s.TitleVi,
            s.ContextVi,
            s.Track.ToString(),
            s.Level.ToString(),
            s.TaskCount,
            passedBySet.GetValueOrDefault(s.Id)))];
    }

    public async Task<WritingSetDetail?> GetSetAsync(
        Guid userId, string code, CancellationToken ct = default)
    {
        var set = await db.WritingSets
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == code, ct);

        if (set is null)
        {
            return null;
        }

        var tasks = await db.WritingTasks
            .AsNoTracking()
            .Where(t => t.SetId == set.Id)
            .OrderBy(t => t.OrderIndex)
            .ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToList();

        // Điểm cao nhất và "đã từng đạt", KHÔNG phải lần nộp gần nhất.
        //
        // Bộ đếm ở danh sách bộ bài đếm theo bài đã từng đạt. Nếu chỗ này lấy lần nộp cuối
        // thì học viên đạt rồi làm lại bị điểm thấp sẽ thấy thẻ bộ ghi "3/6 đạt" trong khi
        // không bài nào mang nhãn đạt — hai con số đá nhau trên cùng một màn hình.
        var stats = await db.WritingAttempts
            .AsNoTracking()
            .Where(a => a.UserId == userId && taskIds.Contains(a.TaskId))
            .GroupBy(a => a.TaskId)
            .Select(g => new
            {
                TaskId = g.Key,
                Best = g.Max(a => a.Score),
                EverPassed = g.Any(a => a.Passed),
            })
            .ToListAsync(ct);

        var byTask = stats.ToDictionary(s => s.TaskId);

        return new WritingSetDetail(
            set.Code,
            set.TitleVi,
            set.ContextVi,
            set.Track.ToString(),
            set.Level.ToString(),
            [.. tasks.Select(t =>
            {
                var prompt = ReadPrompt(t.PromptJson);
                var seen = byTask.GetValueOrDefault(t.Id);

                return new WritingTaskView(
                    t.Code,
                    t.Kind.ToString(),
                    t.PassScore,
                    prompt.PromptVi,
                    prompt.PromptEn,
                    prompt.HintVi,
                    prompt.Fragments,
                    prompt.BlankCount,
                    seen?.Best,
                    seen?.EverPassed ?? false);
            })]);
    }

    /// <summary>
    /// Chấm và lưu một bài nộp. Trả null khi không có bài nào mang cặp mã này —
    /// cùng một câu trả lời cho mã bài sai và mã bộ sai, không phân biệt.
    /// </summary>
    public async Task<WritingSubmitResult?> SubmitAsync(
        Guid userId,
        string setCode,
        string taskCode,
        IReadOnlyList<string> answers,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var task = await db.WritingTasks
            .AsNoTracking()
            .Include(t => t.Set)
            .FirstOrDefaultAsync(t => t.Code == taskCode && t.Set!.Code == setCode, ct);

        if (task is null)
        {
            return null;
        }

        var rubric = ReadRubric(task.Kind, task.RubricJson);
        var grade = _grader.Grade(rubric, answers, task.PassScore);

        db.WritingAttempts.Add(new WritingAttempt
        {
            UserId = userId,
            TaskId = task.Id,
            SubmittedAt = now,
            Score = grade.Score,
            Passed = grade.Passed,
            SubmissionJson = JsonSerializer.Serialize(answers, Json),
            FeedbackJson = JsonSerializer.Serialize(new { grade.FeedbackVi, grade.Score }, Json),
        });

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Học viên {UserId} nộp bài viết {Set}/{Task}, điểm {Score}",
            userId, setCode, taskCode, grade.Score);

        return new WritingSubmitResult(grade.Score, grade.Passed, grade.FeedbackVi, grade.SampleEn);
    }

    private static StoredPrompt ReadPrompt(string json) =>
        JsonSerializer.Deserialize<StoredPrompt>(json, Json) ?? new();

    private static WritingRubric ReadRubric(WritingTaskKind kind, string json)
    {
        var stored = JsonSerializer.Deserialize<StoredRubric>(json, Json) ?? new();

        return new WritingRubric(
            kind,
            stored.Blanks,
            stored.CorrectOrder,
            stored.RequiredPoints,
            stored.SampleEn);
    }

    /// <summary>Hình dạng cột prompt_json. Mọi trường ở đây đều đi ra ngoài được.</summary>
    private sealed class StoredPrompt
    {
        public string PromptVi { get; set; } = string.Empty;
        public string? PromptEn { get; set; }
        public string? HintVi { get; set; }
        public List<string> Fragments { get; set; } = [];
        public int BlankCount { get; set; }
    }

    /// <summary>Hình dạng cột rubric_json. KHÔNG trường nào ở đây được đi ra ngoài trước khi nộp.</summary>
    private sealed class StoredRubric
    {
        public List<List<string>> Blanks { get; set; } = [];
        public List<string> CorrectOrder { get; set; } = [];
        public List<string> RequiredPoints { get; set; } = [];
        public string SampleEn { get; set; } = string.Empty;
    }
}
