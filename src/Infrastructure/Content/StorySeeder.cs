using System.Text.Json;
using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Content;

/// <summary>
/// Nạp chương truyện từ content/story/*.yaml.
///
/// Sửa nội dung chương là an toàn tuyệt đối: <c>story_progresses</c> trỏ khoá ngoại vào
/// chương chứ không sao chép nội dung, nên viết lại thân chương không đụng gì tới việc
/// học viên đã mở hay đã đọc chương đó chưa.
/// </summary>
public class StorySeeder(
    AppDbContext db,
    YamlContentLoader loader,
    StoryValidator validator,
    ILogger<StorySeeder> logger)
{
    public async Task<SeedReport> SeedAsync(string contentRoot, CancellationToken ct = default)
    {
        var problems = new List<string>();

        var load = loader.LoadStoryChapters(contentRoot);
        problems.AddRange(load.Errors.Select(e => $"Doc chuong that bai {Path.GetFileName(e.FilePath)}: {e.Message}"));

        var docs = load.Chapters.Select(c => c.Document).ToList();

        if (docs.Count == 0)
        {
            logger.LogWarning("Không có chương truyện nào trong {Root}/story", contentRoot);
            return new SeedReport(0, 0, 0, 0, problems);
        }

        // Mốc mở chương tra trên danh sách bài có thật: một mã sai chính tả làm chương
        // khoá vĩnh viễn mà không có thông báo nào ở phía học viên.
        var lessonCodes = await db.Lessons
            .Select(l => l.Code)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, ct);

        var issues = validator.ValidateSet(docs, lessonCodes).ToList();

        if (issues.Count > 0)
        {
            problems.AddRange(issues.Select(i => i.ToString()));
            logger.LogError("Chương truyện không qua cổng chất lượng, huỷ seed. {Count} vấn đề", issues.Count);

            return new SeedReport(0, 0, 0, load.Chapters.Count, problems);
        }

        var existing = await db.StoryChapters
            .ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, ct);

        int inserted = 0, updated = 0, unchanged = 0;

        foreach (var loaded in load.Chapters)
        {
            var doc = loaded.Document;

            if (existing.TryGetValue(doc.Code, out var chapter))
            {
                if (chapter.SourceHash == loaded.SourceHash)
                {
                    unchanged++;
                    continue;
                }

                Apply(chapter, doc, loaded.SourceHash);
                updated++;
            }
            else
            {
                chapter = new StoryChapter
                {
                    Code = doc.Code,
                    TitleVi = doc.TitleVi,
                    HookVi = doc.HookVi,
                    BodyVi = doc.BodyVi,
                    EndsVi = doc.EndsVi,
                    NewCharactersJson = "[]",
                    UnlockAfterLessonCode = doc.UnlockAfterLesson,
                    SourceHash = loaded.SourceHash,
                };

                Apply(chapter, doc, loaded.SourceHash);
                db.StoryChapters.Add(chapter);

                inserted++;
            }
        }

        await db.SaveChangesAsync(ct);

        var report = new SeedReport(inserted, updated, unchanged, 0, problems);
        logger.LogInformation("Seed truyện xong: {Report}", report);

        return report;
    }

    private static void Apply(StoryChapter chapter, StoryDocument doc, string hash)
    {
        chapter.Code = doc.Code;
        chapter.Number = doc.Number;
        chapter.TitleVi = doc.TitleVi;
        chapter.HookVi = doc.HookVi;
        chapter.BodyVi = doc.BodyVi.Trim();
        chapter.EndsVi = doc.EndsVi;
        chapter.Track = doc.Track;
        chapter.UnlockAfterLessonCode = doc.UnlockAfterLesson;
        chapter.NewCharactersJson = JsonSerializer.Serialize(doc.NewCharacters);
        chapter.SourceHash = hash;
    }
}
