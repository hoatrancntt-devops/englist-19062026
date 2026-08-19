using System.Text.Json;
using System.Text.Json.Serialization;
using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Content;

/// <summary>
/// Nạp bộ bài luyện viết từ content/writing/*.yaml.
///
/// Khác seeder roleplay ở một điểm quan trọng: <c>writing_attempts</c> CÓ khoá ngoại trỏ vào
/// <c>writing_tasks</c>, nên xoá rồi dựng lại task sẽ cuốn theo toàn bộ bài học viên đã nộp.
/// Vì vậy task được cập nhật tại chỗ theo mã, chỉ xoá những task đã bị gỡ khỏi file.
/// </summary>
public class WritingSeeder(
    AppDbContext db,
    YamlContentLoader loader,
    WritingValidator validator,
    ILogger<WritingSeeder> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<SeedReport> SeedAsync(string contentRoot, CancellationToken ct = default)
    {
        var problems = new List<string>();

        var load = loader.LoadWritingSets(contentRoot);
        problems.AddRange(load.Errors.Select(e => $"Doc bo bai viet that bai {Path.GetFileName(e.FilePath)}: {e.Message}"));

        var docs = load.Sets.Select(s => s.Document).ToList();

        if (docs.Count == 0)
        {
            logger.LogWarning("Không có bộ bài viết nào trong {Root}/writing", contentRoot);
            return new SeedReport(0, 0, 0, 0, problems);
        }

        var issues = validator.ValidateSet(docs).ToList();

        if (issues.Count > 0)
        {
            problems.AddRange(issues.Select(i => i.ToString()));
            logger.LogError("Bộ bài viết không qua cổng chất lượng, huỷ seed. {Count} vấn đề", issues.Count);

            return new SeedReport(0, 0, 0, load.Sets.Count, problems);
        }

        var existing = await db.WritingSets
            .ToDictionaryAsync(s => s.Code, StringComparer.OrdinalIgnoreCase, ct);

        int inserted = 0, updated = 0, unchanged = 0;

        foreach (var loaded in load.Sets)
        {
            var doc = loaded.Document;

            if (existing.TryGetValue(doc.Code, out var set))
            {
                if (set.SourceHash == loaded.SourceHash)
                {
                    unchanged++;
                    continue;
                }

                Apply(set, doc, loaded.SourceHash);
                await SyncTasksAsync(set, doc, ct);

                updated++;
            }
            else
            {
                set = new WritingSet
                {
                    Code = doc.Code,
                    TitleVi = doc.TitleVi,
                    ContextVi = doc.ContextVi,
                    SourceHash = loaded.SourceHash,
                };

                Apply(set, doc, loaded.SourceHash);
                db.WritingSets.Add(set);
                await SyncTasksAsync(set, doc, ct);

                inserted++;
            }
        }

        await db.SaveChangesAsync(ct);

        var report = new SeedReport(inserted, updated, unchanged, 0, problems);
        logger.LogInformation("Seed bài viết xong: {Report}", report);

        return report;
    }

    private static void Apply(WritingSet set, WritingSetDocument doc, string hash)
    {
        set.Code = doc.Code;
        set.TitleVi = doc.TitleVi;
        set.ContextVi = doc.ContextVi;
        set.Track = doc.Track;
        set.Level = doc.Level;
        set.SourceHash = hash;
    }

    /// <summary>
    /// Cập nhật task theo mã thay vì xoá sạch rồi dựng lại.
    ///
    /// Xoá sạch là cách seeder roleplay dùng và nó an toàn ở đó vì không bảng nào trỏ vào node.
    /// Ở đây thì có: mỗi task bị xoá kéo theo mọi bài học viên từng nộp cho nó.
    /// </summary>
    private async Task SyncTasksAsync(WritingSet set, WritingSetDocument doc, CancellationToken ct)
    {
        var current = await db.WritingTasks
            .Where(t => t.SetId == set.Id)
            .ToDictionaryAsync(t => t.Code, StringComparer.OrdinalIgnoreCase, ct);

        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < doc.Tasks.Count; i++)
        {
            var taskDoc = doc.Tasks[i];
            keep.Add(taskDoc.Code);

            // Đề và đáp án tách hẳn hai cột ngay từ lúc seed.
            //
            // Bước viết trong bài học gộp cả hai vào một payload rồi gửi nguyên xuống trình duyệt,
            // nên Blanks và CorrectOrder của nó đọc được bằng công cụ nhà phát triển. Bộ drill
            // không lặp lại chuyện đó: muốn lộ đáp án ở đây thì phải cố ý đọc thêm một cột nữa.
            var promptJson = JsonSerializer.Serialize(
                new
                {
                    kind = taskDoc.Kind.ToString(),
                    promptVi = taskDoc.PromptVi,
                    promptEn = taskDoc.PromptEn,
                    hintVi = taskDoc.HintVi,
                    fragments = taskDoc.Fragments,
                    blankCount = taskDoc.Blanks.Count,
                },
                Json);

            var rubricJson = JsonSerializer.Serialize(
                new
                {
                    kind = taskDoc.Kind.ToString(),
                    blanks = taskDoc.Blanks,
                    correctOrder = taskDoc.CorrectOrder,
                    requiredPoints = taskDoc.RequiredPoints,
                    sampleEn = taskDoc.SampleEn,
                },
                Json);

            if (current.TryGetValue(taskDoc.Code, out var task))
            {
                task.OrderIndex = i;
                task.Kind = taskDoc.Kind;
                task.PromptJson = promptJson;
                task.RubricJson = rubricJson;
                task.PassScore = taskDoc.PassScore;
            }
            else
            {
                db.WritingTasks.Add(new WritingTask
                {
                    SetId = set.Id,
                    Code = taskDoc.Code,
                    OrderIndex = i,
                    Kind = taskDoc.Kind,
                    PromptJson = promptJson,
                    RubricJson = rubricJson,
                    PassScore = taskDoc.PassScore,
                });
            }
        }

        var removed = current.Keys.Where(code => !keep.Contains(code)).ToList();

        if (removed.Count > 0)
        {
            logger.LogWarning(
                "Bộ {Set}: gỡ {Count} bài khỏi nội dung, bài đã nộp cho chúng sẽ mất theo: {Codes}",
                set.Code, removed.Count, string.Join(", ", removed));

            await db.WritingTasks
                .Where(t => t.SetId == set.Id && removed.Contains(t.Code))
                .ExecuteDeleteAsync(ct);
        }
    }
}
