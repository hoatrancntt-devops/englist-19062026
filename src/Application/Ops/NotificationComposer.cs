using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Ops;

/// <summary>Một thông báo đã soạn xong, chưa gắn với người nhận cụ thể.</summary>
public record ComposedNotification(
    NotificationType Type,
    string TitleVi,
    string BodyVi,
    string? ActionUrl,
    /// <summary>
    /// Khoá gộp. Cùng khoá thì chỉ tồn tại một bản ghi — đây là thứ chặn việc dội
    /// mười thông báo "bài đã mở" khi engine tính lại downstream sau một lần chốt bài.
    /// </summary>
    string DedupeKey);

/// <summary>
/// Soạn nội dung thông báo.
///
/// Tách khỏi phần ghi DB và phần gửi mail vì đây là thứ duy nhất cần đọc kỹ bằng mắt:
/// câu chữ gửi tới học viên mỗi ngày. Thuần tính toán nên test không cần DB.
///
/// Nguyên tắc câu chữ: nói con số cụ thể, không doạ, không dùng dấu chấm than.
/// "Còn 2 câu tới hạn ôn" hữu ích hơn "Đừng quên ôn tập!".
/// </summary>
public static class NotificationComposer
{
    public static ComposedNotification DailyReminder(DateOnly dateLocal, int minutesTarget) => new(
        NotificationType.DailyReminder,
        "Tới giờ học rồi",
        $"Hôm nay bạn chưa học phút nào. Mục tiêu là {minutesTarget} phút — làm một bài cũng đã được một phần ba.",
        "/learn",
        $"daily_reminder:{dateLocal:yyyy-MM-dd}");

    public static ComposedNotification ReviewDue(DateOnly dateLocal, int dueCount) => new(
        NotificationType.ReviewDue,
        $"{dueCount} câu tới hạn ôn",
        dueCount >= 20
            ? $"Bạn đang nợ {dueCount} câu. Ôn 10 câu đầu mất khoảng 5 phút."
            : $"Có {dueCount} câu sắp rơi khỏi trí nhớ. Ôn bây giờ nhanh hơn học lại sau.",
        "/learn/review",
        $"review_due:{dateLocal:yyyy-MM-dd}");

    /// <summary>
    /// Cảnh báo sắp đứt chuỗi.
    ///
    /// Chỉ gửi khi chuỗi đủ dài để đáng tiếc. Doạ mất chuỗi 1 ngày là làm phiền,
    /// không phải nhắc nhở.
    /// </summary>
    public static ComposedNotification StreakWarning(DateOnly dateLocal, int currentStreak, int freezeTokens) => new(
        NotificationType.StreakWarning,
        $"Chuỗi {currentStreak} ngày sắp đứt",
        freezeTokens > 0
            ? $"Hôm nay chưa đủ giờ học. Bạn còn {freezeTokens} lượt nghỉ, nhưng học hôm nay vẫn hơn."
            : "Hôm nay chưa đủ giờ học và bạn không còn lượt nghỉ nào.",
        "/learn",
        $"streak_warning:{dateLocal:yyyy-MM-dd}");

    public static ComposedNotification StreakLost(DateOnly dateLocal, int lostStreak) => new(
        NotificationType.StreakLost,
        "Chuỗi đã đứt",
        $"Chuỗi {lostStreak} ngày dừng lại. Kỷ lục cũ vẫn được giữ, và chuỗi mới bắt đầu từ buổi học tới.",
        "/learn",
        $"streak_lost:{dateLocal:yyyy-MM-dd}");

    public static ComposedNotification LessonUnlocked(string lessonCode, string titleVi) => new(
        NotificationType.LessonUnlocked,
        "Bài mới đã mở",
        $"{lessonCode} · {titleVi} đã đủ điều kiện học.",
        $"/learn/lesson/{lessonCode}",
        $"lesson_unlocked:{lessonCode}");

    public static ComposedNotification WeeklyReport(
        DateOnly weekStartLocal, int minutes, int lessonsMastered, int reviewsDone) => new(
        NotificationType.WeeklyReport,
        "Tuần vừa rồi của bạn",
        $"Học {minutes} phút, thạo {lessonsMastered} bài, ôn {reviewsDone} câu.",
        "/learn",
        $"weekly_report:{IsoWeek(weekStartLocal)}");

    public static ComposedNotification PlacementReady() => new(
        NotificationType.PlacementReady,
        "Làm bài xếp lớp trước khi học",
        "Khoảng 18 phút. Kết quả quyết định bạn bắt đầu từ đâu, làm một lần tiết kiệm hàng tuần học sai chỗ.",
        "/placement",
        "placement_ready");

    /// <summary>Nhãn tuần theo ISO, ví dụ 2026-W34. Dùng làm khoá chống gửi trùng báo cáo tuần.</summary>
    public static string IsoWeek(DateOnly date)
    {
        var week = System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        var year = System.Globalization.ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue));

        return $"{year}-W{week:00}";
    }
}
