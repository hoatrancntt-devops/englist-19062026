using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Learning;

/// <summary>Trạng thái một ngày học, đủ để giao diện nói vì sao chuỗi chưa tăng.</summary>
public record StreakDay(
    DateOnly DateLocal,
    int MinutesStudied,
    int MinutesTarget,
    IReadOnlyList<SkillType> SkillsTouched,
    bool CountsTowardStreak,
    string ReasonVi);

/// <summary>
/// Chuỗi ngày học.
///
/// Hai quyết định sản phẩm định hình chỗ này, và cả hai đều làm chuỗi khó tăng hơn
/// so với cách đếm thông thường: ngày chỉ tính khi đủ số phút mục tiêu <b>và</b> chạm
/// đủ bốn kỹ năng. Học năm phút rồi thoát không giữ được chuỗi; học một kỹ năng cũng vậy.
///
/// "Chạm" khác "được chấm". Bước Nói chưa có bộ chấm nhưng học viên vẫn làm nó, nên nó
/// vẫn tính là đã chạm — nếu đòi có điểm thì không ai xây được chuỗi cho tới khi
/// dịch vụ chấm phát âm chạy.
/// </summary>
public class StreakService(
    AppDbContext db,
    IOptions<LearningPolicyOptions> policyOptions,
    ILogger<StreakService> logger)
{
    private readonly LearningPolicyOptions _policy = policyOptions.Value;

    /// <summary>
    /// Tính lại chuỗi sau khi học viên vừa học xong thứ gì đó.
    ///
    /// Gọi được bao nhiêu lần trong ngày cũng không sao: hàm này tính lại từ dữ liệu
    /// của ngày hôm đó chứ không cộng dồn, nên gọi hai lần không cho hai ngày.
    /// </summary>
    public async Task<StreakDay> RecordStudyAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var timeZone = profile?.TimeZone;
        var target = profile?.DailyMinutesTarget ?? _policy.DailyMinutesTarget;

        var today = LocalDay.DateFor(now, timeZone);
        var day = await BuildDayAsync(userId, today, timeZone, target, ct);

        if (!day.CountsTowardStreak)
        {
            return day;
        }

        var streak = await db.Streaks.FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (streak is null)
        {
            streak = new Streak { UserId = userId };
            db.Streaks.Add(streak);
        }

        if (streak.LastStudyDateLocal == today)
        {
            return day;   // Đã tính rồi, không cộng lần hai.
        }

        var gap = streak.LastStudyDateLocal is null
            ? int.MaxValue
            : today.DayNumber - streak.LastStudyDateLocal.Value.DayNumber;

        if (gap == 1)
        {
            streak.CurrentStreak++;
        }
        else if (gap > 1 && streak.LastStudyDateLocal is not null)
        {
            // Nghỉ mấy ngày thì tiêu bấy nhiêu vé. Đủ vé thì chuỗi đi tiếp, thiếu thì về 1.
            var missed = gap - 1;

            if (streak.FreezeTokens >= missed)
            {
                streak.FreezeTokens -= missed;
                streak.CurrentStreak++;

                logger.LogInformation(
                    "Học viên {UserId} dùng {Missed} vé nghỉ để giữ chuỗi {Streak}",
                    userId, missed, streak.CurrentStreak);
            }
            else
            {
                streak.CurrentStreak = 1;
            }
        }
        else
        {
            streak.CurrentStreak = 1;
        }

        streak.LastStudyDateLocal = today;
        streak.LongestStreak = Math.Max(streak.LongestStreak, streak.CurrentStreak);

        await db.SaveChangesAsync(ct);

        return day;
    }

    /// <summary>Trạng thái hôm nay, không ghi gì. Bảng điều khiển dùng để giải thích.</summary>
    public async Task<StreakDay> TodayAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);

        return await BuildDayAsync(
            userId,
            LocalDay.DateFor(now, profile?.TimeZone),
            profile?.TimeZone,
            profile?.DailyMinutesTarget ?? _policy.DailyMinutesTarget,
            ct);
    }

    private async Task<StreakDay> BuildDayAsync(
        Guid userId, DateOnly dateLocal, string? timeZone, int target, CancellationToken ct)
    {
        var zone = LocalDay.Resolve(timeZone);

        // .ToUniversalTime() là bắt buộc, không phải cho gọn: Npgsql ánh xạ DateTimeOffset
        // sang timestamptz và TỪ CHỐI mọi giá trị có offset khác 0. Nửa đêm giờ Việt Nam
        // mang offset +07 nên truy vấn ném lỗi ngay — DB trong bộ nhớ thì cho qua, nên
        // lỗi này chỉ lộ ra ở test tích hợp.
        var startUtc = new DateTimeOffset(
            dateLocal.ToDateTime(TimeOnly.MinValue),
            zone.GetUtcOffset(dateLocal.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();

        var endUtc = startUtc.AddDays(1);

        // Đọc CẢ bản ghi chưa chấm: câu hỏi ở đây là "có làm không", không phải "được mấy điểm".
        var attempts = await db.ActivityAttempts
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.CreatedAt >= startUtc && a.CreatedAt < endUtc)
            .Select(a => new { a.Skill, a.DurationSeconds })
            .ToListAsync(ct);

        var minutes = (int)Math.Round(attempts.Sum(a => a.DurationSeconds) / 60.0);
        var skills = attempts.Select(a => a.Skill).Distinct().OrderBy(s => s).ToList();

        var allSkills = Enum.GetValues<SkillType>().ToList();
        var missingSkills = allSkills.Except(skills).ToList();

        var minutesOk = !_policy.StreakRequiresDailyTarget || minutes >= target;
        var skillsOk = !_policy.StreakRequiresAllFourSkills || missingSkills.Count == 0;

        return new StreakDay(
            dateLocal,
            minutes,
            target,
            skills,
            minutesOk && skillsOk,
            BuildReason(minutesOk, skillsOk, minutes, target, missingSkills));
    }

    private static string BuildReason(
        bool minutesOk, bool skillsOk, int minutes, int target, IReadOnlyList<SkillType> missing)
    {
        if (minutesOk && skillsOk)
        {
            return "Hôm nay đã tính vào chuỗi.";
        }

        var parts = new List<string>();

        if (!minutesOk)
        {
            parts.Add($"còn thiếu {target - minutes} phút");
        }

        if (!skillsOk)
        {
            parts.Add("chưa chạm " + string.Join(", ", missing.Select(SkillNameVi)));
        }

        return "Chưa tính vào chuỗi: " + string.Join("; ", parts) + ".";
    }

    private static string SkillNameVi(SkillType skill) => skill switch
    {
        SkillType.Listening => "Nghe",
        SkillType.Speaking => "Nói",
        SkillType.Reading => "Đọc",
        SkillType.Writing => "Viết",
        _ => skill.ToString(),
    };
}
