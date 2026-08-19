using EnglishForIT.Domain.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishForIT.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.TitleVi).HasMaxLength(200).IsRequired();
        b.Property(x => x.BodyVi).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ActionUrl).HasMaxLength(400);
        b.Property(x => x.DedupeKey).HasMaxLength(200).IsRequired();

        // Chống dội: cùng khoá gộp thì không tạo bản ghi mới.
        b.HasIndex(x => x.DedupeKey).IsUnique();

        // Đếm chưa đọc cho chuông ở header.
        b.HasIndex(x => new { x.UserId, x.ReadAt })
            .HasFilter("read_at IS NULL");

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> b)
    {
        b.HasIndex(x => x.UserId).IsUnique();

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_notification_preferences_quiet_start",
                "quiet_hours_start BETWEEN 0 AND 23");
            t.HasCheckConstraint("ck_notification_preferences_quiet_end",
                "quiet_hours_end BETWEEN 0 AND 23");
        });
    }
}

public class OutboxEmailConfiguration : IEntityTypeConfiguration<OutboxEmail>
{
    public void Configure(EntityTypeBuilder<OutboxEmail> b)
    {
        b.Property(x => x.ToAddress).HasMaxLength(320).IsRequired();
        b.Property(x => x.ToDisplayName).HasMaxLength(120);
        b.Property(x => x.Subject).HasMaxLength(400).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        b.UseXminAsConcurrencyToken();

        // Gửi trùng một báo cáo tuần hai lần là lỗi người dùng thấy được, nên chặn ở DB.
        b.HasIndex(x => x.IdempotencyKey).IsUnique();

        // Worker quét thư chờ gửi theo index này.
        b.HasIndex(x => new { x.Status, x.NextAttemptAt })
            .HasFilter("status = 'Pending'");
    }
}

public class MailSettingConfiguration : IEntityTypeConfiguration<MailSetting>
{
    public void Configure(EntityTypeBuilder<MailSetting> b)
    {
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.FromAddress).HasMaxLength(320).IsRequired();
        b.Property(x => x.FromDisplayName).HasMaxLength(120).IsRequired();
        b.Property(x => x.TenantId).HasMaxLength(64);
        b.Property(x => x.ClientId).HasMaxLength(64);
        b.Property(x => x.ClientSecretEncrypted).HasMaxLength(2000);
        b.Property(x => x.SmtpHost).HasMaxLength(255);
        b.Property(x => x.SmtpUsername).HasMaxLength(320);
        b.Property(x => x.SmtpPasswordEncrypted).HasMaxLength(2000);
        b.Property(x => x.LastTestError).HasMaxLength(2000);
    }
}

public class AiCacheEntryConfiguration : IEntityTypeConfiguration<AiCacheEntry>
{
    public void Configure(EntityTypeBuilder<AiCacheEntry> b)
    {
        b.Property(x => x.CacheKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.TaskName).HasMaxLength(64).IsRequired();
        b.Property(x => x.PromptVersion).HasMaxLength(32).IsRequired();
        b.Property(x => x.ResponseJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Model).HasMaxLength(120).IsRequired();

        b.HasIndex(x => x.CacheKey).IsUnique();

        // Job dọn dẹp xoá bản hết hạn theo cột này.
        b.HasIndex(x => x.ExpiresAt);
    }
}

public class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    public void Configure(EntityTypeBuilder<AiUsage> b)
    {
        b.Property(x => x.TaskName).HasMaxLength(64).IsRequired();
        b.Property(x => x.Tier).HasMaxLength(8).IsRequired();
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Model).HasMaxLength(120).IsRequired();
        b.Property(x => x.ErrorCode).HasMaxLength(64);
        b.Property(x => x.EstimatedCostUsd).HasPrecision(12, 6);

        // Tính chi phí tháng để quyết định hạ cấp ngân sách.
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}

public class AiProviderSettingConfiguration : IEntityTypeConfiguration<AiProviderSetting>
{
    public void Configure(EntityTypeBuilder<AiProviderSetting> b)
    {
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.ApiKeyEncrypted).HasMaxLength(2000);
        b.Property(x => x.BaseUrl).HasMaxLength(500);
        b.Property(x => x.ExtraJson).HasColumnType("jsonb");

        b.HasIndex(x => x.Provider).IsUnique();
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.Action).HasMaxLength(64).IsRequired();
        b.Property(x => x.TargetType).HasMaxLength(64).IsRequired();
        b.Property(x => x.TargetId).HasMaxLength(64);
        b.Property(x => x.MetadataJson).HasColumnType("jsonb");
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(256);

        b.HasIndex(x => new { x.Action, x.CreatedAt });
        b.HasIndex(x => new { x.ActorUserId, x.CreatedAt });
    }
}

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.Property(x => x.Key).HasMaxLength(128).IsRequired();
        b.Property(x => x.ValueJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.DescriptionVi).HasMaxLength(500);

        b.HasIndex(x => x.Key).IsUnique();
    }
}
