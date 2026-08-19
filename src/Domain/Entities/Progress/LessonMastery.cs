using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Progress;

/// <summary>Ghi danh khoá học. Một user một enrollment cho bản này, nhưng để sẵn cho nhiều khoá về sau.</summary>
public class Enrollment : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Bậc lúc bắt đầu, do xếp lớp đặt. Giữ nguyên để đo tiến bộ.</summary>
    public CefrLevel EntryLevel { get; set; }

    /// <summary>Bài được mở đầu tiên sau xếp lớp.</summary>
    public Guid? EntryLessonId { get; set; }
}

/// <summary>
/// Tiến độ của một học viên trên một bài. Đây là bảng nóng nhất hệ thống —
/// engine chống nhảy cóc đọc và ghi liên tục.
/// </summary>
public class LessonMastery : Entity, IConcurrencyStamped
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public LessonState State { get; set; } = LessonState.Locked;

    /// <summary>Mastery thô, trung bình có trọng số của các trục kỹ năng, thang 0-100.</summary>
    public double MasteryRaw { get; set; }

    /// <summary>
    /// Mastery hiệu dụng sau khi trừ suy giảm theo thời gian.
    /// Đây là con số engine dùng để quyết định mở khoá.
    /// </summary>
    public double MasteryEffective { get; set; }

    /// <summary>Điểm từng kỹ năng, JSONB {"Listening":72,...}. Ngưỡng xét riêng từng trục.</summary>
    public Dictionary<SkillType, double> SkillScores { get; set; } = [];

    public int AttemptsCount { get; set; }
    public DateTimeOffset? FirstStartedAt { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public DateTimeOffset? MasteredAt { get; set; }

    /// <summary>Tổng số giây đã học bài này. Dùng để hiệu chỉnh dự báo thời gian.</summary>
    public int TimeSpentSeconds { get; set; }

    /// <summary>Đã qua bằng thi vượt chứ không phải học tuần tự.</summary>
    public bool UnlockedByChallenge { get; set; }

    public uint RowVersion { get; set; }
}

/// <summary>
/// Nhật ký đổi trạng thái. Màn hình "vì sao bài này bị khoá" đọc bảng này,
/// nên nó không bao giờ phải đoán ngược từ điểm số.
/// </summary>
public class LessonStateEvent : Entity
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }

    public LessonState FromState { get; set; }
    public LessonState ToState { get; set; }
    public LessonStateReason Reason { get; set; }

    /// <summary>
    /// Chi tiết đủ để render câu giải thích, JSONB. Ví dụ:
    /// {"missing":[{"lesson":"INF-02","need":60,"have":41}]}
    /// </summary>
    public required string DetailJson { get; set; }
}

/// <summary>Một lần làm bài. Có autosave nên bản ghi tồn tại trước khi nộp.</summary>
public class LessonAttempt : Entity
{
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Bước đang làm dở, để mở lại đúng chỗ.</summary>
    public int CurrentActivityIndex { get; set; }

    /// <summary>Trạng thái autosave, JSONB. Ghi đè mỗi 10 giây phía client.</summary>
    public string? DraftStateJson { get; set; }

    public double? Score { get; set; }
}

/// <summary>Kết quả một bước trong bài. Đây là nguồn để tính SkillScores.</summary>
public class ActivityAttempt : Entity
{
    public Guid UserId { get; set; }
    public Guid LessonAttemptId { get; set; }
    public Guid ActivityId { get; set; }

    public ActivityKind Kind { get; set; }
    public SkillType Skill { get; set; }

    public double Score { get; set; }
    public bool Passed { get; set; }

    /// <summary>
    /// Bước này đã chấm được chưa.
    ///
    /// Tách "đã làm" khỏi "có điểm" vì hai câu hỏi khác nhau cần hai câu trả lời khác nhau:
    /// chuỗi ngày hỏi học viên có chạm đủ bốn kỹ năng không, còn mastery hỏi họ được bao nhiêu điểm.
    /// Bước Nói hiện chưa có bộ chấm nên nó <b>có</b> bản ghi (đã làm) nhưng <c>Graded = false</c>,
    /// và mọi chỗ tính điểm phải lọc bỏ nó — Score 0 ở đây không có nghĩa là làm sai.
    /// </summary>
    public bool Graded { get; set; } = true;

    /// <summary>Chi tiết chấm, JSONB. Với bước nói là điểm ba trục và âm vị sai.</summary>
    public required string ResultJson { get; set; }

    public int DurationSeconds { get; set; }
}
