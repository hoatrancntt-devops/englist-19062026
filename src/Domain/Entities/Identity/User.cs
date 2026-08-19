using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Identity;

/// <summary>
/// Tài khoản đăng nhập. Không bao giờ chứa mật khẩu thô —
/// chỉ chứa chuỗi Argon2id đã đóng gói tham số trong <see cref="PasswordHash"/>.
/// </summary>
public class User : Entity, ISoftDelete, IConcurrencyStamped
{
    /// <summary>Đã chuẩn hoá về chữ thường khi ghi. Unique có phân biệt bản ghi xoá mềm.</summary>
    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>Chuỗi Argon2id dạng $argon2id$v=19$m=...,t=...,p=...$salt$hash</summary>
    public required string PasswordHash { get; set; }

    public bool EmailVerified { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }

    /// <summary>Khoá đăng nhập tạm sau quá nhiều lần sai. Null nghĩa là không bị khoá.</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Mốc vô hiệu hoá toàn bộ phiên cũ. Đổi mật khẩu thì đẩy mốc này lên,
    /// mọi session tạo trước đó thành vô hiệu mà không cần xoá từng dòng.
    /// </summary>
    public DateTimeOffset SecurityStamp { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DeletedAt { get; set; }
    public uint RowVersion { get; set; }

    public UserProfile? Profile { get; set; }
    public ICollection<UserRoleAssignment> Roles { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}

/// <summary>Gán vai trò. Một user có thể giữ nhiều vai (support kiêm content_editor).</summary>
public class UserRoleAssignment : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public UserRole Role { get; set; }

    /// <summary>Ai cấp vai này. Null nghĩa là hệ thống cấp lúc seed.</summary>
    public Guid? GrantedByUserId { get; set; }
}
