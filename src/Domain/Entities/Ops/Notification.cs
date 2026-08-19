using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Ops;

/// <summary>Thông báo trong ứng dụng. Chuông ở header đọc bảng này.</summary>
public class Notification : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public NotificationType Type { get; set; }

    public required string TitleVi { get; set; }
    public required string BodyVi { get; set; }

    /// <summary>Đường dẫn khi bấm vào, ví dụ /learn/lesson/INF-03.</summary>
    public string? ActionUrl { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>
    /// Khoá gộp. Cùng khoá trong cùng ngày thì không tạo bản ghi mới —
    /// chống dội mười thông báo "bài đã mở" khi engine tính lại downstream.
    /// </summary>
    public required string DedupeKey { get; set; }
}

/// <summary>Tuỳ chọn nhận thông báo. Một dòng cho một user.</summary>
public class NotificationPreference : Entity
{
    public Guid UserId { get; set; }

    public bool DailyReminderEnabled { get; set; } = true;
    public bool StreakAlertsEnabled { get; set; } = true;
    public bool ReviewDueEnabled { get; set; } = true;
    public bool WeeklyReportEnabled { get; set; } = true;

    /// <summary>Gửi email ngoài thông báo trong ứng dụng.</summary>
    public bool EmailEnabled { get; set; } = true;

    /// <summary>Giờ địa phương bắt đầu không làm phiền, 0-23.</summary>
    public int QuietHoursStart { get; set; } = 22;
    public int QuietHoursEnd { get; set; } = 7;
}

/// <summary>
/// Hộp thư đi. Ghi vào DB trước, worker gửi sau — mất kết nối SMTP không làm mất thư.
/// </summary>
public class OutboxEmail : Entity, IConcurrencyStamped
{
    public required string ToAddress { get; set; }
    public string? ToDisplayName { get; set; }

    public required string Subject { get; set; }
    public required string HtmlBody { get; set; }
    public string? TextBody { get; set; }

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Lỗi lần gửi gần nhất. Đã lọc bỏ mọi thứ giống bí mật trước khi ghi.</summary>
    public string? LastError { get; set; }

    /// <summary>Khoá chống gửi trùng, ví dụ weekly_report:userId:2026-W33.</summary>
    public required string IdempotencyKey { get; set; }

    public uint RowVersion { get; set; }
}

/// <summary>
/// Cấu hình gửi thư, nhập trên web chứ không qua biến môi trường.
/// Client secret và mật khẩu SMTP mã hoá trước khi ghi.
/// </summary>
public class MailSetting : Entity
{
    public MailProvider Provider { get; set; } = MailProvider.Smtp;

    public bool Enabled { get; set; }

    public required string FromAddress { get; set; }
    public required string FromDisplayName { get; set; }

    // Microsoft Graph
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }

    /// <summary>Đã mã hoá. Không bao giờ trả nguyên văn ra API.</summary>
    public string? ClientSecretEncrypted { get; set; }

    // SMTP
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public bool SmtpUseStartTls { get; set; } = true;
    public string? SmtpUsername { get; set; }

    /// <summary>Đã mã hoá.</summary>
    public string? SmtpPasswordEncrypted { get; set; }

    public DateTimeOffset? LastTestAt { get; set; }
    public bool? LastTestSucceeded { get; set; }
    public string? LastTestError { get; set; }
}
