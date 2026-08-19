using EnglishForIT.Application.Abstractions;
using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Application.Identity;

public class AuthOptions
{
    public int SessionLifetimeDays { get; set; } = 30;
    public int PasswordResetTtlMinutes { get; set; } = 30;
    public int EmailVerificationTtlHours { get; set; } = 48;

    /// <summary>Số lần sai liên tiếp trước khi khoá tạm.</summary>
    public int MaxFailedLogins { get; set; } = 8;

    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Độ dài mật khẩu tối thiểu. NIST khuyến nghị dài hơn là quan trọng hơn phức tạp hơn.</summary>
    public int MinPasswordLength { get; set; } = 10;
}

/// <summary>Kết quả đăng nhập. Thất bại không bao giờ nói rõ vì sao.</summary>
public record AuthResult(bool Success, string? SessionToken, string? CsrfToken, Guid? UserId, string? FailureReasonInternal)
{
    public static AuthResult Fail(string internalReason) => new(false, null, null, null, internalReason);
    public static AuthResult Ok(string token, string csrf, Guid userId) => new(true, token, csrf, userId, null);
}

public interface IAuthService
{
    Task<Guid> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default);
    Task<AuthResult> AuthenticateAsync(string email, string password, string? ip, string? userAgent, CancellationToken ct = default);
    Task<Session?> ResolveSessionAsync(string sessionToken, CancellationToken ct = default);
    Task RevokeSessionAsync(string sessionToken, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task EnsureAdminAsync(string email, string password, string displayName, CancellationToken ct = default);
    Task<int> PurgeExpiredSessionsAsync(CancellationToken ct = default);
}

/// <summary>
/// Xác thực bằng phiên lưu server. Không dùng JWT: thu hồi tức thì quan trọng hơn stateless,
/// và ứng dụng này chỉ có một backend nên không cần token tự chứa.
/// </summary>
public class AuthService(
    IAuthDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IClock clock,
    IOptions<AuthOptions> options,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly AuthOptions _o = options.Value;

    /// <summary>
    /// Hash giả dùng khi email không tồn tại. Giữ cho hai nhánh đăng nhập tốn thời gian
    /// tương đương, để không ai dò được email nào đã đăng ký.
    /// </summary>
    private static readonly string DummyHash =
        "$argon2id$v=19$m=32768,t=3,p=2$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public async Task<Guid> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);

        if (password.Length < _o.MinPasswordLength)
        {
            throw new ArgumentException($"Mật khẩu phải dài ít nhất {_o.MinPasswordLength} ký tự.", nameof(password));
        }

        if (await db.Users.AnyAsync(u => u.Email == normalized, ct))
        {
            // Ném lỗi có nội dung ở tầng service; tầng API sẽ đổi thành thông báo chung
            // để trang đăng ký không thành công cụ dò email.
            throw new InvalidOperationException("Email đã tồn tại.");
        }

        var user = new User
        {
            Email = normalized,
            DisplayName = displayName.Trim(),
            PasswordHash = hasher.Hash(password),
            SecurityStamp = clock.UtcNow
        };

        db.Users.Add(user);

        // Tạo sẵn hồ sơ và các bản ghi phụ thuộc: mọi truy vấn sau đó không phải xử lý null.
        db.UserProfiles.Add(new UserProfile { UserId = user.Id });
        db.UserRoles.Add(new UserRoleAssignment { UserId = user.Id, Role = UserRole.Learner });
        db.NotificationPreferences.Add(new NotificationPreference { UserId = user.Id });
        db.Streaks.Add(new Streak { UserId = user.Id });

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Đã tạo tài khoản {UserId}", user.Id);
        return user.Id;
    }

    public async Task<AuthResult> AuthenticateAsync(
        string email, string password, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        var now = clock.UtcNow;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);

        // Email không tồn tại: vẫn băm một lần với hash giả rồi mới trả về thất bại.
        if (user is null)
        {
            hasher.Verify(password, DummyHash);
            return AuthResult.Fail("unknown_email");
        }

        if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            hasher.Verify(password, DummyHash);
            return AuthResult.Fail("locked");
        }

        if (!hasher.Verify(password, user.PasswordHash))
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= _o.MaxFailedLogins)
            {
                user.LockedUntil = now.AddMinutes(_o.LockoutMinutes);
                user.FailedLoginCount = 0;
                logger.LogWarning("Khoá tạm tài khoản {UserId} sau quá nhiều lần sai", user.Id);
            }

            await db.SaveChangesAsync(ct);
            return AuthResult.Fail("bad_password");
        }

        // Tham số băm đã nâng từ lần đăng nhập trước thì băm lại ngay, người dùng không thấy gì.
        if (hasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = hasher.Hash(password);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;

        var (token, session) = CreateSession(user.Id, ip, userAgent, now);
        db.Sessions.Add(session);

        await db.SaveChangesAsync(ct);

        return AuthResult.Ok(token, session.CsrfSecret, user.Id);
    }

    public async Task<Session?> ResolveSessionAsync(string sessionToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var hash = tokens.HashToken(sessionToken);
        var now = clock.UtcNow;

        var session = await db.Sessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);

        if (session is null || !session.IsActive(now) || session.User is null)
        {
            return null;
        }

        // Đổi mật khẩu đẩy SecurityStamp lên; mọi phiên tạo trước mốc đó thành vô hiệu
        // mà không phải đi xoá từng dòng.
        if (session.CreatedAt < session.User.SecurityStamp)
        {
            return null;
        }

        // Cập nhật LastSeenAt tối đa mỗi 5 phút một lần, để mỗi request không sinh một lệnh ghi.
        if (now - session.LastSeenAt > TimeSpan.FromMinutes(5))
        {
            session.LastSeenAt = now;
            await db.SaveChangesAsync(ct);
        }

        return session;
    }

    public async Task RevokeSessionAsync(string sessionToken, CancellationToken ct = default)
    {
        var hash = tokens.HashToken(sessionToken);

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == hash, ct);
        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.RevokedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (newPassword.Length < _o.MinPasswordLength)
        {
            throw new ArgumentException($"Mật khẩu phải dài ít nhất {_o.MinPasswordLength} ký tự.", nameof(newPassword));
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !hasher.Verify(currentPassword, user.PasswordHash))
        {
            return false;
        }

        var now = clock.UtcNow;

        user.PasswordHash = hasher.Hash(newPassword);

        // Đẩy mốc bảo mật: mọi phiên cũ, kể cả trên thiết bị khác, mất hiệu lực ngay.
        user.SecurityStamp = now;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Đổi mật khẩu thành công cho {UserId}, mọi phiên cũ đã vô hiệu", userId);
        return true;
    }

    public async Task EnsureAdminAsync(string email, string password, string displayName, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);

        if (user is null)
        {
            var userId = await RegisterAsync(normalized, password, displayName, ct);
            db.UserRoles.Add(new UserRoleAssignment { UserId = userId, Role = UserRole.SuperAdmin });
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Đã tạo tài khoản quản trị {Email}", normalized);
            return;
        }

        // Đã có tài khoản: chỉ bảo đảm có vai trò, KHÔNG đặt lại mật khẩu.
        // Seed lại không được phép ghi đè mật khẩu admin đang dùng.
        var hasRole = await db.UserRoles.AnyAsync(r => r.UserId == user.Id && r.Role == UserRole.SuperAdmin, ct);
        if (!hasRole)
        {
            db.UserRoles.Add(new UserRoleAssignment { UserId = user.Id, Role = UserRole.SuperAdmin });
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> PurgeExpiredSessionsAsync(CancellationToken ct = default)
    {
        var cutoff = clock.UtcNow.AddDays(-7);

        // Giữ phiên hết hạn thêm 7 ngày để còn điều tra khi có sự cố bảo mật.
        return await db.Sessions
            .Where(s => s.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }

    private (string Token, Session Session) CreateSession(Guid userId, string? ip, string? userAgent, DateTimeOffset now)
    {
        var token = tokens.GenerateToken();

        var session = new Session
        {
            UserId = userId,
            TokenHash = tokens.HashToken(token),
            CsrfSecret = tokens.GenerateToken(24),
            ExpiresAt = now.AddDays(_o.SessionLifetimeDays),
            LastSeenAt = now,
            IpAddress = ip,
            UserAgent = userAgent?.Length > 256 ? userAgent[..256] : userAgent
        };

        return (token, session);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
