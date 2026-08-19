using System.Text.Json;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Learning;

/// <summary>
/// Một chương trong danh sách. Chương chưa mở vẫn hiện tiêu đề và câu mở —
/// đó chính là thứ tạo ra mong muốn học tiếp — nhưng KHÔNG kèm thân chương.
/// </summary>
public record StoryChapterSummary(
    string Code,
    int Number,
    string TitleVi,
    string HookVi,
    string Track,
    bool Unlocked,
    DateTimeOffset? UnlockedAt,
    DateTimeOffset? ReadAt,
    /// <summary>Bài phải thông thạo để mở chương. Hiện nguyên văn khi chương còn khoá.</summary>
    string UnlockAfterLessonCode,
    string? UnlockAfterLessonTitle,
    IReadOnlyList<string> NewCharacters);

/// <summary>Chương đã mở, kèm thân và câu kết.</summary>
public record StoryChapterDetail(
    string Code,
    int Number,
    string TitleVi,
    string HookVi,
    string BodyVi,
    string EndsVi,
    string Track,
    DateTimeOffset? ReadAt,
    IReadOnlyList<string> NewCharacters);

/// <summary>
/// Mạch truyện xuyên suốt khoá học.
///
/// Chương mở khi học viên thông thạo bài làm mốc. Không có nút mở bằng tay, không mở theo
/// thời gian: mở tự do thì học viên đọc hết trong một buổi và mạch truyện mất sạch tác dụng
/// giữ chân — đó là toàn bộ lý do tính năng này tồn tại.
///
/// Thân chương không rời máy chủ khi chương còn khoá. Cùng kỷ luật với nhãn chất lượng của
/// roleplay và đáp án của bài thi vượt.
/// </summary>
public class StoryService(AppDbContext db, ILogger<StoryService> logger)
{
    public async Task<IReadOnlyList<StoryChapterSummary>> ListAsync(
        Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var chapters = await db.StoryChapters
            .AsNoTracking()
            .OrderBy(c => c.Number)
            .ToListAsync(ct);

        if (chapters.Count == 0)
        {
            return [];
        }

        var anchors = chapters.Select(c => c.UnlockAfterLessonCode).Distinct().ToList();

        var anchorLessons = await db.Lessons
            .AsNoTracking()
            .Where(l => anchors.Contains(l.Code))
            .Select(l => new { l.Id, l.Code, l.TitleVi })
            .ToListAsync(ct);

        var lessonTitleByCode = anchorLessons.ToDictionary(
            l => l.Code, l => l.TitleVi, StringComparer.OrdinalIgnoreCase);

        var anchorIds = anchorLessons.Select(l => l.Id).ToList();

        var masteredLessonIds = await db.LessonMasteries
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.MasteredAt != null && anchorIds.Contains(m.LessonId))
            .Select(m => m.LessonId)
            .ToHashSetAsync(ct);

        var masteredCodes = anchorLessons
            .Where(l => masteredLessonIds.Contains(l.Id))
            .Select(l => l.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var progress = await db.StoryProgresses
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.ChapterId, ct);

        var newlyUnlocked = 0;

        foreach (var chapter in chapters.Where(c => masteredCodes.Contains(c.UnlockAfterLessonCode)))
        {
            if (progress.TryGetValue(chapter.Id, out var row))
            {
                if (row.Unlocked)
                {
                    continue;
                }

                row.Unlocked = true;
                row.UnlockedAt = now;
            }
            else
            {
                var created = new StoryProgress
                {
                    UserId = userId,
                    ChapterId = chapter.Id,
                    Unlocked = true,
                    UnlockedAt = now,
                };

                db.StoryProgresses.Add(created);
                progress[chapter.Id] = created;
            }

            newlyUnlocked++;
        }

        if (newlyUnlocked > 0)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Mở {Count} chương truyện cho học viên {UserId}", newlyUnlocked, userId);
            }
            catch (DbUpdateException)
            {
                // Hai request cùng lúc cùng thấy chương vừa đủ điều kiện mở thì cùng chèn một
                // dòng, và ràng buộc duy nhất (UserId, ChapterId) chặn dòng thứ hai.
                //
                // Chuyện này xảy ra đúng vào lúc chương mở ra — thời điểm tệ nhất để trang
                // truyện trả lỗi. Request kia đã ghi xong rồi, nên bỏ thay đổi của mình và
                // đọc lại là đủ, không cần báo gì cho học viên.
                foreach (var entry in db.ChangeTracker.Entries<StoryProgress>().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                progress = await db.StoryProgresses
                    .AsNoTracking()
                    .Where(p => p.UserId == userId)
                    .ToDictionaryAsync(p => p.ChapterId, ct);

                logger.LogInformation(
                    "Một request khác đã mở chương cho học viên {UserId} trước, đọc lại trạng thái", userId);
            }
        }

        return [.. chapters.Select(c =>
        {
            progress.TryGetValue(c.Id, out var row);

            return new StoryChapterSummary(
                c.Code,
                c.Number,
                c.TitleVi,
                c.HookVi,
                c.Track.ToString(),
                row?.Unlocked ?? false,
                row?.UnlockedAt,
                row?.ReadAt,
                c.UnlockAfterLessonCode,
                lessonTitleByCode.GetValueOrDefault(c.UnlockAfterLessonCode),
                ReadCharacters(c.NewCharactersJson));
        })];
    }

    /// <summary>
    /// Mở một chương để đọc. Trả null khi chương chưa mở hoặc không tồn tại — cùng một
    /// câu trả lời cho cả hai, để không lộ có bao nhiêu chương phía trước.
    /// </summary>
    public async Task<StoryChapterDetail?> ReadAsync(
        Guid userId, string code, DateTimeOffset now, CancellationToken ct = default)
    {
        var chapter = await db.StoryChapters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == code, ct);

        if (chapter is null)
        {
            return null;
        }

        var row = await db.StoryProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ChapterId == chapter.Id, ct);

        if (row is null || !row.Unlocked)
        {
            return null;
        }

        // Chỉ ghi mốc đọc lần đầu. Đọc lại không dời mốc: nó dùng để biết chương nào
        // học viên đã bỏ qua, không phải để đếm lượt xem.
        if (row.ReadAt is null)
        {
            row.ReadAt = now;
            await db.SaveChangesAsync(ct);
        }

        return new StoryChapterDetail(
            chapter.Code,
            chapter.Number,
            chapter.TitleVi,
            chapter.HookVi,
            chapter.BodyVi,
            chapter.EndsVi,
            chapter.Track.ToString(),
            row.ReadAt,
            ReadCharacters(chapter.NewCharactersJson));
    }

    private static IReadOnlyList<string> ReadCharacters(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            // Nhân vật hỏng không được làm chết cả danh sách chương.
            return [];
        }
    }
}
