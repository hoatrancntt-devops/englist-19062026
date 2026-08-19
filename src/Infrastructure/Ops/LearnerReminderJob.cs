using EnglishForIT.Application.Learning;
using EnglishForIT.Application.Ops;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Infrastructure.Learning;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Ops;

public record ReminderRunReport(int Checked, int Reminded, int ReviewNudged, int StreaksBroken);

/// <summary>
/// Job nhắc học chạy mỗi giờ.
///
/// Chạy mỗi giờ chứ không mỗi ngày vì học viên ở nhiều múi giờ: "20 giờ tối" của mỗi người
/// rơi vào một giờ UTC khác nhau. Mỗi lượt chạy chỉ chạm những người mà giờ địa phương
/// của họ đúng bằng giờ họ đã chọn.
///
/// Mọi thứ ở đây đều dựa trên khoá gộp theo ngày, nên chạy lại nhiều lần trong cùng một giờ
/// cũng không gửi trùng.
/// </summary>
public class LearnerReminderJob(
    AppDbContext db,
    StreakService streaks,
    NotificationService notifications,
    IOptions<LearningPolicyOptions> policyOptions,
    ILogger<LearnerReminderJob> logger)
{
    private readonly LearningPolicyOptions _policy = policyOptions.Value;

    public async Task<ReminderRunReport> RunAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var profiles = await db.UserProfiles
            .AsNoTracking()
            .Select(p => new { p.UserId, p.TimeZone, p.ReminderHourLocal, p.DailyMinutesTarget })
            .ToListAsync(ct);

        int reminded = 0, nudged = 0, broken = 0;

        foreach (var profile in profiles)
        {
            var localHour = LocalDay.HourFor(now, profile.TimeZone);
            var localDate = LocalDay.DateFor(now, profile.TimeZone);

            // Đứt chuỗi xử lý ngay sau nửa đêm địa phương, tách khỏi giờ nhắc học:
            // hai việc này trả lời hai câu hỏi khác nhau vào hai thời điểm khác nhau.
            if (localHour == 0 && await BreakStreakIfMissedAsync(profile.UserId, localDate, now, ct))
            {
                broken++;
            }

            if (localHour != profile.ReminderHourLocal)
            {
                continue;
            }

            var day = await streaks.TodayAsync(profile.UserId, now, ct);

            if (day.MinutesStudied == 0)
            {
                if (await notifications.PushAsync(
                        profile.UserId,
                        NotificationComposer.DailyReminder(localDate, profile.DailyMinutesTarget),
                        now, ct: ct))
                {
                    reminded++;
                }
            }

            var dueCount = await db.ReviewQueue
                .CountAsync(r => r.UserId == profile.UserId && r.DueAt <= now, ct);

            if (dueCount > 0
                && await notifications.PushAsync(
                    profile.UserId, NotificationComposer.ReviewDue(localDate, dueCount), now, ct: ct))
            {
                nudged++;
            }
        }

        await db.SaveChangesAsync(ct);

        var report = new ReminderRunReport(profiles.Count, reminded, nudged, broken);

        if (reminded + nudged + broken > 0)
        {
            logger.LogInformation("Nhắc học: {Report}", report);
        }

        return report;
    }

    /// <summary>
    /// Xử lý chuỗi khi học viên bỏ lỡ ngày hôm qua.
    ///
    /// Tiêu một vé nghỉ nếu còn, hết vé thì chuỗi về 0 và báo cho họ biết. Không im lặng
    /// đặt lại: mất chuỗi mà không hiểu vì sao là cách nhanh nhất để họ bỏ app.
    /// </summary>
    private async Task<bool> BreakStreakIfMissedAsync(
        Guid userId, DateOnly todayLocal, DateTimeOffset now, CancellationToken ct)
    {
        var streak = await db.Streaks.FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (streak is null || streak.CurrentStreak == 0 || streak.LastStudyDateLocal is null)
        {
            return false;
        }

        var gap = todayLocal.DayNumber - streak.LastStudyDateLocal.Value.DayNumber;

        // gap 0 là học hôm nay, gap 1 là học hôm qua — cả hai đều chưa đứt.
        if (gap <= 1)
        {
            return false;
        }

        var missed = gap - 1;

        if (streak.FreezeTokens >= missed)
        {
            streak.FreezeTokens -= missed;
            streak.LastStudyDateLocal = todayLocal.AddDays(-1);   // coi như đã học hôm qua

            logger.LogInformation("Học viên {UserId} tiêu {Missed} vé nghỉ để giữ chuỗi", userId, missed);
            return false;
        }

        var lost = streak.CurrentStreak;
        streak.CurrentStreak = 0;

        await notifications.PushAsync(
            userId, NotificationComposer.StreakLost(todayLocal, lost), now, ignoreQuietHours: true, ct);

        return true;
    }

    /// <summary>
    /// Cấp vé nghỉ hàng tuần, trần theo chính sách.
    ///
    /// Chạy thứ Hai theo giờ địa phương của từng người, và chỉ cấp một lần mỗi tuần —
    /// mốc kiểm là <see cref="Streak.LastFreezeGrantedAt"/> chứ không phải "hôm nay là thứ Hai",
    /// vì job chạy mỗi giờ nên thứ Hai có tới 24 lượt chạy.
    /// </summary>
    public async Task<int> GrantWeeklyFreezeAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var profiles = await db.UserProfiles
            .AsNoTracking()
            .Select(p => new { p.UserId, p.TimeZone })
            .ToListAsync(ct);

        var granted = 0;

        foreach (var profile in profiles)
        {
            var local = LocalDay.ToLocal(now, profile.TimeZone);

            if (local.DayOfWeek != DayOfWeek.Monday)
            {
                continue;
            }

            var streak = await db.Streaks.FirstOrDefaultAsync(s => s.UserId == profile.UserId, ct);

            if (streak is null || streak.FreezeTokens >= _policy.MaxStreakFreezeTokens)
            {
                continue;
            }

            if (streak.LastFreezeGrantedAt is not null
                && (now - streak.LastFreezeGrantedAt.Value).TotalDays < 6)
            {
                continue;
            }

            streak.FreezeTokens++;
            streak.LastFreezeGrantedAt = now;
            granted++;
        }

        if (granted > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Cấp {Count} vé nghỉ tuần này", granted);
        }

        return granted;
    }
}
