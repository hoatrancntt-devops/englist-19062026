using EnglishForIT.Application.Learning;
using EnglishForIT.Application.Ops;
using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Ops;

/// <summary>
/// Ghi thông báo cho học viên.
///
/// Ba luật chặn, theo thứ tự kiểm: học viên đã tắt loại đó chưa, đang trong giờ không làm
/// phiền không, và khoá gộp đã tồn tại chưa. Thiếu luật thứ ba thì một lần chốt bài làm
/// engine tính lại downstream và học viên nhận mười thông báo "bài đã mở" liền nhau.
/// </summary>
public class NotificationService(AppDbContext db, ILogger<NotificationService> logger)
{
    /// <summary>
    /// Gửi một thông báo. Trả về false khi bị chặn — gọi bao nhiêu lần cũng an toàn.
    /// </summary>
    /// <param name="ignoreQuietHours">
    /// Dùng cho loại thông báo do chính học viên vừa gây ra (ví dụ bài mở khoá ngay sau khi
    /// họ chốt bài): họ đang mở app, chặn theo giờ yên tĩnh là vô nghĩa.
    /// </param>
    public async Task<bool> PushAsync(
        Guid userId,
        ComposedNotification notification,
        DateTimeOffset now,
        bool ignoreQuietHours = false,
        CancellationToken ct = default)
    {
        var prefs = await db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (!IsEnabled(prefs, notification.Type))
        {
            return false;
        }

        if (!ignoreQuietHours && prefs is not null)
        {
            var timeZone = await db.UserProfiles
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => p.TimeZone)
                .FirstOrDefaultAsync(ct);

            var hour = LocalDay.HourFor(now, timeZone);

            if (LocalDay.IsQuietHour(hour, prefs.QuietHoursStart, prefs.QuietHoursEnd))
            {
                return false;
            }
        }

        var exists = await db.Notifications
            .AnyAsync(n => n.UserId == userId && n.DedupeKey == notification.DedupeKey, ct);

        if (exists)
        {
            return false;
        }

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = notification.Type,
            TitleVi = notification.TitleVi,
            BodyVi = notification.BodyVi,
            ActionUrl = notification.ActionUrl,
            DedupeKey = notification.DedupeKey,
        });

        logger.LogInformation(
            "Thông báo {Type} cho học viên {UserId}, khoá {Key}",
            notification.Type, userId, notification.DedupeKey);

        return true;
    }

    /// <summary>
    /// Xếp một email vào hộp thư đi.
    ///
    /// Ghi DB trước, worker gửi sau: mất kết nối SMTP không được làm mất thư. Khoá chống
    /// gửi trùng dùng chung dạng với khoá gộp của thông báo trong ứng dụng.
    /// </summary>
    public async Task<bool> QueueEmailAsync(
        Guid userId,
        string toAddress,
        string subject,
        string htmlBody,
        string textBody,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var prefs = await db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (prefs is not null && !prefs.EmailEnabled)
        {
            return false;
        }

        var exists = await db.OutboxEmails.AnyAsync(e => e.IdempotencyKey == idempotencyKey, ct);

        if (exists)
        {
            return false;
        }

        db.OutboxEmails.Add(new OutboxEmail
        {
            ToAddress = toAddress,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            IdempotencyKey = idempotencyKey,
            NextAttemptAt = DateTimeOffset.UtcNow,
        });

        return true;
    }

    /// <summary>
    /// Loại thông báo này có được bật không.
    ///
    /// Chưa có dòng tuỳ chọn nghĩa là chưa tắt gì — mặc định bật hết, đúng như giá trị
    /// mặc định của <see cref="NotificationPreference"/>.
    /// </summary>
    private static bool IsEnabled(NotificationPreference? prefs, NotificationType type)
    {
        if (prefs is null)
        {
            return true;
        }

        return type switch
        {
            NotificationType.DailyReminder => prefs.DailyReminderEnabled,
            NotificationType.StreakWarning or NotificationType.StreakLost => prefs.StreakAlertsEnabled,
            NotificationType.ReviewDue or NotificationType.RetentionDebt => prefs.ReviewDueEnabled,
            NotificationType.WeeklyReport => prefs.WeeklyReportEnabled,

            // Bài mở khoá, qua chốt chặng và nhắc xếp lớp không tắt được: chúng là
            // thông tin về tiến độ, không phải quảng cáo.
            _ => true,
        };
    }
}
