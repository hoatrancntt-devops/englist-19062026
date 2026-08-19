using EnglishForIT.Domain.Common;

namespace EnglishForIT.Domain.Entities.Identity;

/// <summary>
/// Phiên đăng nhập. Cookie giữ token thô; DB chỉ giữ SHA-256 của token.
/// Lộ DB không đủ để mạo danh phiên.
/// </summary>
public class Session : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>SHA-256 của token phiên, hex thường, 64 ký tự.</summary>
    public required string TokenHash { get; set; }

    /// <summary>Bí mật CSRF gắn với phiên, dùng cho double-submit cookie.</summary>
    public required string CsrfSecret { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Phiên thay thế phiên này khi xoay vòng. Cho phép truy vết chuỗi phiên.</summary>
    public Guid? RotatedToSessionId { get; set; }

    /// <summary>Cắt còn 256 ký tự khi ghi. Chỉ để người dùng nhận ra thiết bị lạ.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Lưu dạng inet. Không dùng để phân quyền, chỉ để audit.</summary>
    public string? IpAddress { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}

/// <summary>
/// Token một lần cho đặt lại mật khẩu và xác minh email.
/// Chỉ lưu hash, có TTL, dùng xong đánh dấu ConsumedAt.
/// </summary>
public abstract class OneTimeToken : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>SHA-256 của token gửi qua email.</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;
}

public class PasswordResetToken : OneTimeToken;

public class EmailVerificationToken : OneTimeToken;
