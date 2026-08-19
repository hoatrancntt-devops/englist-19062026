using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Entities.Progress;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.Application.Abstractions;

/// <summary>
/// Cửa hẹp mà module xác thực nhìn thấy DB. Application không tham chiếu Infrastructure,
/// nên tầng này chỉ khai báo cái nó cần và Infrastructure cài đặt.
/// </summary>
public interface IAuthDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<UserRoleAssignment> UserRoles { get; }
    DbSet<Session> Sessions { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<EmailVerificationToken> EmailVerificationTokens { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<Streak> Streaks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
