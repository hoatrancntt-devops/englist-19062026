using System.Text.Json;
using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Learning;

/// <summary>Một câu trong buổi ôn. Chỉ phần đề bài — đáp án ở lại máy chủ.</summary>
public record ReviewCard(
    string ItemCode,
    string LessonCode,
    string LessonTitleVi,
    JsonElement Prompt,
    /// <summary>Quá hạn bao nhiêu ngày. 0 nghĩa là đến hạn hôm nay.</summary>
    int OverdueDays,
    /// <summary>Đã ôn đúng bao nhiêu lần liên tiếp. Học viên thấy được mình đang xây gì.</summary>
    int RepetitionCount);

public record ReviewSession(
    IReadOnlyList<ReviewCard> Cards,
    /// <summary>Tổng số câu đang tới hạn, kể cả phần vượt trần một buổi.</summary>
    int TotalDue,
    /// <summary>Câu tới hạn gần nhất khi hàng đợi đang rỗng. Null nghĩa là chưa có gì xếp lịch.</summary>
    DateTimeOffset? NextDueAt,
    string MessageVi);

public record ReviewAnswerResult(
    bool Correct,
    int CorrectIndex,
    /// <summary>Số ngày tới lần ôn kế tiếp của chính câu này.</summary>
    int NextIntervalDays,
    int RemainingDue,
    string MessageVi);

/// <summary>
/// Buổi ôn tập giãn cách.
///
/// Ba quyết định định hình toàn bộ lớp này:
///
/// Một, ôn tập KHÔNG đụng vào mastery của bài. Trả lời sai chỉ kéo lịch câu đó về một ngày.
/// Nếu để nó hạ điểm bài thì một buổi ôn tệ có thể tụt bài đã đạt xuống dưới ngưỡng,
/// và engine chống nhảy cóc sẽ khoá dây chuyền các bài phía sau — tức là phạt học viên
/// vì đã chịu khó ôn. Không ai ôn lần thứ hai sau khi bị như vậy một lần.
///
/// Hai, chấm từng câu chứ không gom cuối buổi. Bỏ dở giữa chừng thì các câu đã làm
/// vẫn được xếp lịch xong.
///
/// Ba, mỗi buổi có trần. Hàng đợi hai trăm câu hiện nguyên khối là bức tường
/// không ai bấm vào.
/// </summary>
public class ReviewService(AppDbContext db, ILogger<ReviewService> logger)
{
    /// <summary>
    /// Số câu tối đa một buổi.
    ///
    /// Hai mươi câu rơi vào khoảng năm phút — vừa một buổi ôn xen giữa hai bài học,
    /// và đủ ngắn để học viên bấm vào lúc chỉ còn ít thời gian.
    /// </summary>
    public const int SessionSize = 20;

    public async Task<ReviewSession> GetSessionAsync(
        Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var dueQuery = db.ReviewQueue
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.DueAt <= now);

        var totalDue = await dueQuery.CountAsync(ct);

        if (totalDue == 0)
        {
            var nextDueAt = await db.ReviewQueue
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderBy(r => r.DueAt)
                .Select(r => (DateTimeOffset?)r.DueAt)
                .FirstOrDefaultAsync(ct);

            return new ReviewSession([], 0, nextDueAt, EmptyMessage(nextDueAt, now));
        }

        // Quá hạn lâu nhất trước: đó là những câu thật sự sắp rơi khỏi trí nhớ.
        // Cùng mức quá hạn thì ưu tiên câu có hệ số giãn thấp — câu đang khó với người này.
        var rows = await dueQuery
            .OrderBy(r => r.DueAt)
            .ThenBy(r => r.Ease)
            .Take(SessionSize)
            .Select(r => new
            {
                r.ItemId,
                r.DueAt,
                r.RepetitionCount,
                ItemCode = r.Item!.Code,
                r.Item!.PromptJson,
                LessonCode = r.Item!.Activity!.Lesson!.Code,
                LessonTitleVi = r.Item!.Activity!.Lesson!.TitleVi,
            })
            .ToListAsync(ct);

        var cards = rows
            .Select(r => new ReviewCard(
                r.ItemCode,
                r.LessonCode,
                r.LessonTitleVi,
                JsonDocument.Parse(r.PromptJson).RootElement.Clone(),
                Math.Max(0, (int)(now - r.DueAt).TotalDays),
                r.RepetitionCount))
            .ToList();

        return new ReviewSession(cards, totalDue, null, SessionMessage(cards.Count, totalDue));
    }

    /// <summary>
    /// Chấm một câu và xếp lịch lại ngay.
    ///
    /// Công thức trùng với chỗ chốt bài trong <see cref="LessonPlayerService"/>: đúng thì
    /// khoảng cách nhân hệ số giãn, sai thì về một ngày. Giữ hai chỗ giống nhau là có chủ đích —
    /// một câu ôn đúng và một câu làm đúng trong bài phải được xếp lịch như nhau, nếu không
    /// thì đường cong ghi nhớ của học viên phụ thuộc vào chỗ họ tình cờ gặp câu đó.
    /// </summary>
    public async Task<ReviewAnswerResult?> SubmitAnswerAsync(
        Guid userId, string itemCode, int chosenIndex, DateTimeOffset now, CancellationToken ct = default)
    {
        var row = await db.ReviewQueue
            .Include(r => r.Item)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Item!.Code == itemCode, ct);

        if (row?.Item is null)
        {
            // Câu không nằm trong hàng đợi của chính người này. Trả null để tầng trên
            // ra 404 — không tiết lộ câu đó có tồn tại hay không.
            logger.LogWarning("Ôn tập: không thấy câu {ItemCode} trong hàng đợi của người dùng", itemCode);
            return null;
        }

        var correctIndex = ReadCorrectIndex(row.Item.AnswerJson);
        var correct = chosenIndex == correctIndex;

        if (correct)
        {
            row.RepetitionCount++;
            row.IntervalDays = Math.Clamp((int)Math.Round(row.IntervalDays * row.Ease), 1, 60);
            row.Ease = Math.Min(3.0, row.Ease + 0.05);
        }
        else
        {
            row.LapseCount++;
            row.IntervalDays = 1;
            row.Ease = Math.Max(1.3, row.Ease - 0.2);
        }

        row.DueAt = now.AddDays(row.IntervalDays);
        row.LastReviewedAt = now;

        await db.SaveChangesAsync(ct);

        var remaining = await db.ReviewQueue
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId && r.DueAt <= now, ct);

        return new ReviewAnswerResult(
            correct,
            correctIndex,
            row.IntervalDays,
            remaining,
            AnswerMessage(correct, row.IntervalDays));
    }

    private static string EmptyMessage(DateTimeOffset? nextDueAt, DateTimeOffset now)
    {
        if (nextDueAt is null)
        {
            return "Chưa có câu nào trong hàng đợi. Học xong một bài là các câu của bài đó "
                 + "được xếp lịch ôn tự động.";
        }

        var days = Math.Max(1, (int)Math.Ceiling((nextDueAt.Value - now).TotalDays));

        return days == 1
            ? "Hôm nay không còn câu nào tới hạn. Câu tiếp theo sẽ đến vào ngày mai."
            : $"Hôm nay không còn câu nào tới hạn. Câu tiếp theo sẽ đến sau {days} ngày.";
    }

    private static string SessionMessage(int inSession, int totalDue)
    {
        return totalDue > inSession
            ? $"{totalDue} câu đang tới hạn. Buổi này lấy {inSession} câu quá hạn lâu nhất — "
            + "phần còn lại vẫn nằm trong hàng đợi."
            : $"{inSession} câu đang tới hạn.";
    }

    private static string AnswerMessage(bool correct, int intervalDays)
    {
        if (!correct)
        {
            return "Chưa đúng. Câu này sẽ quay lại vào ngày mai.";
        }

        return intervalDays == 1
            ? "Đúng. Câu này quay lại vào ngày mai."
            : $"Đúng. Câu này sẽ quay lại sau {intervalDays} ngày.";
    }

    /// <summary>
    /// Đọc chỉ số đáp án đúng.
    ///
    /// Tra tên thuộc tính không phân biệt hoa thường, cùng lý do đã ghi trong
    /// <see cref="ActivityGrader"/>: bộ ghi dùng PascalCase còn JsonDocument thì phân biệt,
    /// và lệch một chữ hoa khiến mọi câu bị chấm sai mà không ném lỗi nào.
    /// </summary>
    private static int ReadCorrectIndex(string answerJson)
    {
        using var doc = JsonDocument.Parse(answerJson);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "answer", StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.TryGetInt32(out var index) ? index : -1;
            }
        }

        return -1;
    }
}
