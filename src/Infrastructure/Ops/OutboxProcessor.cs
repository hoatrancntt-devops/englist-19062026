using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Ops;

/// <summary>
/// Gửi các thư đang chờ trong hộp thư đi.
///
/// Thất bại thì lùi lịch theo cấp số nhân chứ không thử lại ngay: SMTP hỏng thường hỏng
/// vài phút, và thử lại liên tục chỉ làm log ngập và có khi bị nhà cung cấp chặn IP.
/// Quá số lần thì đánh Failed và dừng — thư kẹt mãi trong hàng đợi còn tệ hơn thư mất,
/// vì không ai biết nó tồn tại.
/// </summary>
public class OutboxProcessor(
    AppDbContext db,
    IEmailSender sender,
    ILogger<OutboxProcessor> logger)
{
    /// <summary>Số lần thử tối đa. Lần 5 rơi vào khoảng 4 tiếng sau lần đầu.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Số thư xử lý mỗi lượt. Đủ nhỏ để một lượt không chạy quá lâu.</summary>
    public const int BatchSize = 20;

    public async Task<int> ProcessAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var pending = await db.OutboxEmails
            .Where(e => e.Status == OutboxStatus.Pending
                        && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .OrderBy(e => e.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return 0;
        }

        var sent = 0;

        foreach (var email in pending)
        {
            var result = await sender.SendAsync(email, ct);

            email.AttemptCount++;

            if (result.Success)
            {
                email.Status = OutboxStatus.Sent;
                email.SentAt = now;
                email.LastError = null;
                sent++;
            }
            else if (email.AttemptCount >= MaxAttempts)
            {
                email.Status = OutboxStatus.Failed;
                email.LastError = Truncate(result.Error);

                logger.LogError(
                    "Thư {Id} tới {To} bỏ cuộc sau {Attempts} lần: {Error}",
                    email.Id, email.ToAddress, email.AttemptCount, email.LastError);
            }
            else
            {
                email.LastError = Truncate(result.Error);
                email.NextAttemptAt = now.Add(BackoffFor(email.AttemptCount));
            }
        }

        await db.SaveChangesAsync(ct);

        return sent;
    }

    /// <summary>Lùi lịch: 1, 5, 25, 125 phút. Lần thử cuối rơi vào khoảng 4 tiếng sau lần đầu.</summary>
    public static TimeSpan BackoffFor(int attemptCount) =>
        TimeSpan.FromMinutes(Math.Pow(5, Math.Max(0, attemptCount - 1)));

    /// <summary>
    /// Cắt ngắn lỗi trước khi ghi. Cột này hiện trên màn quản trị nên không được
    /// mang theo chuỗi kết nối hay bất cứ thứ gì dài bất thường.
    /// </summary>
    private static string? Truncate(string? error) =>
        error is null ? null : error.Length <= 500 ? error : error[..500];
}
