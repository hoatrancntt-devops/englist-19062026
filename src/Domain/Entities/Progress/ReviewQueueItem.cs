using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Progress;

/// <summary>
/// Hàng đợi ôn tập giãn cách, xếp lịch theo từng item chứ không theo cả bài.
/// Công thức: interval mới = min(60, interval cũ nhân ease).
/// </summary>
public class ReviewQueueItem : Entity, IConcurrencyStamped
{
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public LessonItem? Item { get; set; }

    public DateTimeOffset DueAt { get; set; }

    /// <summary>Khoảng cách hiện tại tính bằng ngày. Trần cứng 60 ngày.</summary>
    public int IntervalDays { get; set; } = 1;

    /// <summary>Hệ số giãn. Trả lời đúng thì tăng nhẹ, sai thì kéo về 1.3.</summary>
    public double Ease { get; set; } = 2.5;

    public int RepetitionCount { get; set; }
    public int LapseCount { get; set; }

    public DateTimeOffset? LastReviewedAt { get; set; }

    public uint RowVersion { get; set; }
}

/// <summary>Chuỗi ngày học liên tiếp. Một dòng cho một user.</summary>
public class Streak : Entity, IConcurrencyStamped
{
    public Guid UserId { get; set; }

    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }

    /// <summary>Ngày học gần nhất theo lịch địa phương của học viên.</summary>
    public DateOnly? LastStudyDateLocal { get; set; }

    /// <summary>
    /// Số lần được phép nghỉ mà không đứt chuỗi. Job thứ Hai cấp thêm một cái,
    /// trần là 2 để không thành vô nghĩa.
    /// </summary>
    public int FreezeTokens { get; set; }

    public DateTimeOffset? LastFreezeGrantedAt { get; set; }

    public uint RowVersion { get; set; }
}

/// <summary>Tiến độ đọc truyện. Chương mở theo phase học, không mở tự do.</summary>
public class StoryProgress : Entity
{
    public Guid UserId { get; set; }

    /// <summary>Khai navigation để có khoá ngoại cascade: xoá học viên phải cuốn theo tiến độ đọc.</summary>
    public User? User { get; set; }

    public Guid ChapterId { get; set; }
    public StoryChapter? Chapter { get; set; }

    public bool Unlocked { get; set; }
    public DateTimeOffset? UnlockedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>Một lượt đóng vai. Lưu đường đi để học viên xem lại mình đã chọn gì.</summary>
public class RoleplayAttempt : Entity
{
    public Guid UserId { get; set; }
    public Guid ScenarioId { get; set; }
    public RoleplayScenario? Scenario { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public RoleplayOutcome Outcome { get; set; } = RoleplayOutcome.Incomplete;

    /// <summary>Chuỗi node đã đi qua và lựa chọn đã chọn, JSONB mảng.</summary>
    public required string PathJson { get; set; }

    /// <summary>Số lần phải xem gợi ý. Cao thì bài chưa vững.</summary>
    public int HintsUsed { get; set; }

    public double Score { get; set; }
}

/// <summary>
/// Vé thi vượt đã đạt. Tách bảng để engine phân biệt "học xong" với "thi vượt qua" —
/// hai thứ này khác nhau khi tính nợ ôn tập.
/// </summary>
public class ChallengePass : Entity
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }

    public double Score { get; set; }
    public DateTimeOffset PassedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Các item đã dùng trong bài thi vượt, JSONB. Chống thi lại đúng bộ câu.</summary>
    public required string ItemCodesJson { get; set; }
}

/// <summary>
/// Một lần nộp bài luyện viết.
///
/// Lưu cả bài học viên viết chứ không chỉ điểm: với dạng viết email, thứ đáng xem lại sau
/// vài tuần là câu chữ mình đã dùng, không phải con số 80.
/// </summary>
public class WritingAttempt : Entity
{
    public Guid UserId { get; set; }

    /// <summary>Khai navigation để có khoá ngoại cascade: xoá học viên phải cuốn theo bài đã nộp.</summary>
    public User? User { get; set; }

    public Guid TaskId { get; set; }
    public WritingTask? Task { get; set; }

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public double Score { get; set; }
    public bool Passed { get; set; }

    /// <summary>Bài đã nộp, JSONB. Hình dạng tuỳ dạng bài: chỗ trống, thứ tự, hoặc đoạn văn.</summary>
    public required string SubmissionJson { get; set; }

    /// <summary>Nhận xét từng ý đã sinh lúc chấm, JSONB. Giữ lại để xem lại không phải chấm lại.</summary>
    public required string FeedbackJson { get; set; }
}
