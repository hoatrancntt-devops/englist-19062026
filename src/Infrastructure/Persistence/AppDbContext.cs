using System.Reflection;
using EnglishForIT.Application.Abstractions;
using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Entities.Progress;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.Infrastructure.Persistence;

/// <summary>
/// DbContext duy nhất của hệ thống. Đây là modular monolith: một schema, nhiều module,
/// ranh giới module giữ bằng quy ước namespace và service, không bằng nhiều DbContext.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAuthDbContext
{
    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserRoleAssignment> UserRoles => Set<UserRoleAssignment>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<OnboardingAnswer> OnboardingAnswers => Set<OnboardingAnswer>();

    // Content
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonPrerequisite> LessonPrerequisites => Set<LessonPrerequisite>();
    public DbSet<LessonActivity> LessonActivities => Set<LessonActivity>();
    public DbSet<LessonItem> LessonItems => Set<LessonItem>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();
    public DbSet<StoryChapter> StoryChapters => Set<StoryChapter>();
    public DbSet<RoleplayScenario> RoleplayScenarios => Set<RoleplayScenario>();
    public DbSet<RoleplayNode> RoleplayNodes => Set<RoleplayNode>();
    public DbSet<PlacementForm> PlacementForms => Set<PlacementForm>();
    public DbSet<PlacementFormItem> PlacementFormItems => Set<PlacementFormItem>();
    public DbSet<WritingSet> WritingSets => Set<WritingSet>();
    public DbSet<WritingTask> WritingTasks => Set<WritingTask>();

    // Progress
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LessonMastery> LessonMasteries => Set<LessonMastery>();
    public DbSet<LessonStateEvent> LessonStateEvents => Set<LessonStateEvent>();
    public DbSet<LessonAttempt> LessonAttempts => Set<LessonAttempt>();
    public DbSet<ActivityAttempt> ActivityAttempts => Set<ActivityAttempt>();
    public DbSet<PlacementAttempt> PlacementAttempts => Set<PlacementAttempt>();
    public DbSet<PlacementAnswer> PlacementAnswers => Set<PlacementAnswer>();
    public DbSet<PlacementSpeakingScore> PlacementSpeakingScores => Set<PlacementSpeakingScore>();
    public DbSet<SpeechAttempt> SpeechAttempts => Set<SpeechAttempt>();
    public DbSet<ReviewQueueItem> ReviewQueue => Set<ReviewQueueItem>();
    public DbSet<Streak> Streaks => Set<Streak>();
    public DbSet<StoryProgress> StoryProgresses => Set<StoryProgress>();
    public DbSet<RoleplayAttempt> RoleplayAttempts => Set<RoleplayAttempt>();
    public DbSet<ChallengePass> ChallengePasses => Set<ChallengePass>();
    public DbSet<WritingAttempt> WritingAttempts => Set<WritingAttempt>();
    public DbSet<ConsolidationPass> ConsolidationPasses => Set<ConsolidationPass>();

    public DbSet<VocabDeck> VocabDecks => Set<VocabDeck>();
    public DbSet<VocabWord> VocabWords => Set<VocabWord>();
    public DbSet<VocabWordProgress> VocabWordProgresses => Set<VocabWordProgress>();

    // Ops
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();
    public DbSet<MailSetting> MailSettings => Set<MailSetting>();
    public DbSet<AiCacheEntry> AiCacheEntries => Set<AiCacheEntry>();
    public DbSet<AiUsage> AiUsages => Set<AiUsage>();
    public DbSet<AiProviderSetting> AiProviderSettings => Set<AiProviderSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tên bảng và cột theo snake_case để truy vấn tay trong psql không phải trích dẫn kép.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()!));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
            }

            foreach (var fk in entity.GetForeignKeys())
            {
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));
            }
        }
    }

    /// <summary>
    /// Cập nhật dấu thời gian tập trung, để không service nào phải nhớ gán UpdatedAt.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Chỉ đóng dấu khi service CHƯA tự đặt.
                    //
                    // Ghi đè vô điều kiện thì tham số `now` mà các service truyền qua lại
                    // thành vô nghĩa ở đường ghi: chúng so sánh bằng giờ logic nhưng lưu
                    // bằng giờ thực. Trên production hai giờ trùng nhau nên không ai thấy,
                    // nhưng mọi cơ chế dựa trên mốc thời gian đã lưu — như hạn chờ thi lại —
                    // thành không kiểm được, và test dùng giờ cố định sẽ đỏ theo giờ trong ngày.
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = now;
                    }

                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    // Không cho sửa CreatedAt kể cả khi service lỡ gán.
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var builder = new System.Text.StringBuilder(input.Length + 8);

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (char.IsUpper(c))
            {
                var previousIsLower = i > 0 && char.IsLower(input[i - 1]);
                var nextIsLower = i + 1 < input.Length && char.IsLower(input[i + 1]);

                if (i > 0 && (previousIsLower || nextIsLower) && builder[^1] != '_')
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
