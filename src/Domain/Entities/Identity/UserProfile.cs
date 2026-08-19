using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Identity;

/// <summary>Hồ sơ học. Tách khỏi <see cref="User"/> để bảng đăng nhập gọn và ít bị ghi.</summary>
public class UserProfile : Entity, IConcurrencyStamped
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Chức danh tự khai, ví dụ "System Engineer". Dùng để chọn ví dụ minh hoạ hợp nghề.</summary>
    public string? JobTitle { get; set; }

    /// <summary>Mục tiêu chọn ở onboarding, lưu JSONB. Nhiều mục tiêu là hợp lệ.</summary>
    public List<LearningGoal> Goals { get; set; } = [];

    /// <summary>Nhánh nghề ưu tiên, suy ra từ Goals nhưng cho phép admin ghi đè.</summary>
    public LearningTrack PrimaryTrack { get; set; } = LearningTrack.Infrastructure;

    /// <summary>
    /// Chế độ học: đủ bốn kỹ năng hay chỉ một kỹ năng.
    /// Đổi chế độ KHÔNG xoá tiến độ đã có — bước đã đạt vẫn giữ nguyên điểm.
    /// </summary>
    public StudyMode StudyMode { get; set; } = StudyMode.Mixed;

    /// <summary>Tầng ngữ cảnh đang học. Học viên mất gốc bắt đầu ở Life.</summary>
    public ContextLayer CurrentLayer { get; set; } = ContextLayer.Life;

    /// <summary>
    /// Mục tiêu phút mỗi ngày. Mặc định 45 — khoảng ba buổi 15 phút.
    /// Giá trị mặc định lấy từ LearningPolicyOptions.DailyMinutesTarget.
    /// </summary>
    public int DailyMinutesTarget { get; set; } = 45;

    /// <summary>Đã qua test micro ở onboarding chưa. Chưa qua thì ẩn phần nói.</summary>
    public bool MicrophoneChecked { get; set; }

    public bool OnboardingCompleted { get; set; }
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    /// <summary>Bậc hiện tại, do xếp lớp đặt và checkpoint nâng.</summary>
    public CefrLevel CurrentLevel { get; set; } = CefrLevel.PreA1;

    /// <summary>IANA timezone, ví dụ Asia/Ho_Chi_Minh. Quyết định giờ gửi nhắc học.</summary>
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>Giờ địa phương muốn nhận nhắc học, 0-23.</summary>
    public int ReminderHourLocal { get; set; } = 20;

    public uint RowVersion { get; set; }
}

/// <summary>
/// Lưu nguyên văn câu trả lời onboarding. Giữ riêng khỏi UserProfile vì đây là dữ liệu thô
/// để phân tích sau, còn UserProfile là trạng thái đang dùng.
/// </summary>
public class OnboardingAnswer : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public required string QuestionKey { get; set; }

    /// <summary>Giá trị JSON, vì câu trả lời có thể là chuỗi, số hoặc mảng.</summary>
    public required string AnswerJson { get; set; }
}
