using EnglishForIT.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishForIT.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();

        // Unique chỉ áp cho tài khoản còn sống: xoá mềm rồi đăng ký lại cùng email là hợp lệ.
        b.HasIndex(x => x.Email)
            .IsUnique()
            .HasFilter("deleted_at IS NULL");
        b.UseXminAsConcurrencyToken();

        b.HasQueryFilter(x => x.DeletedAt == null);

        b.HasOne(x => x.Profile)
            .WithOne(x => x.User!)
            .HasForeignKey<UserProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.Property(x => x.JobTitle).HasMaxLength(160);
        b.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();

        // Mảng enum lưu JSONB: đọc được bằng mắt trong psql, query được bằng toán tử jsonb.
        b.Property(x => x.Goals)
            .HasConversion(JsonbConverters.GoalList, JsonbConverters.GoalListComparer)
            .HasColumnType("jsonb");

        b.Property(x => x.PrimaryTrack).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CurrentLevel).HasConversion<string>().HasMaxLength(16);
        b.UseXminAsConcurrencyToken();

        b.HasIndex(x => x.UserId).IsUnique();

        // Chặn giá trị vô nghĩa ngay tại DB, không phụ thuộc tầng ứng dụng nhớ kiểm.
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_user_profiles_daily_minutes",
                "daily_minutes_target BETWEEN 5 AND 240");
            t.HasCheckConstraint("ck_user_profiles_reminder_hour",
                "reminder_hour_local BETWEEN 0 AND 23");
        });
    }
}

public class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> b)
    {
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);

        b.HasIndex(x => new { x.UserId, x.Role }).IsUnique();

        b.HasOne(x => x.User)
            .WithMany(x => x.Roles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.CsrfSecret).HasMaxLength(64).IsRequired();
        b.Property(x => x.UserAgent).HasMaxLength(256);
        // varchar chứ không phải inet: Npgsql 10 không map string sang inet nữa, và cột này
        // chỉ dùng để audit chứ không truy vấn theo dải mạng.
        b.Property(x => x.IpAddress).HasMaxLength(45);

        b.HasIndex(x => x.TokenHash).IsUnique();

        // Truy vấn nóng nhất: lấy phiên còn sống của một user.
        b.HasIndex(x => new { x.UserId, x.ExpiresAt })
            .HasFilter("revoked_at IS NULL");

        // Job dọn dẹp quét theo cột này.
        b.HasIndex(x => x.ExpiresAt);

        b.HasOne(x => x.User)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.ExpiresAt);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.ExpiresAt);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OnboardingAnswerConfiguration : IEntityTypeConfiguration<OnboardingAnswer>
{
    public void Configure(EntityTypeBuilder<OnboardingAnswer> b)
    {
        b.Property(x => x.QuestionKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.AnswerJson).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => new { x.UserId, x.QuestionKey }).IsUnique();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
