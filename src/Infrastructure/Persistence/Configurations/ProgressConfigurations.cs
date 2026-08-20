using EnglishForIT.Domain.Entities.Progress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishForIT.Infrastructure.Persistence.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.Property(x => x.EntryLevel).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => x.UserId).IsUnique();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LessonMasteryConfiguration : IEntityTypeConfiguration<LessonMastery>
{
    public void Configure(EntityTypeBuilder<LessonMastery> b)
    {
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(16);

        b.Property(x => x.SkillScores)
            .HasConversion(JsonbConverters.SkillScoreMap, JsonbConverters.SkillScoreMapComparer)
            .HasColumnType("jsonb");
        b.UseXminAsConcurrencyToken();

        // Một học viên chỉ có một dòng cho một bài. Đây là ràng buộc giữ engine khỏi
        // tạo bản ghi trùng khi hai request chấm điểm về cùng lúc.
        b.HasIndex(x => new { x.UserId, x.LessonId }).IsUnique();

        // Truy vấn roadmap: lấy toàn bộ trạng thái của một học viên.
        b.HasIndex(x => new { x.UserId, x.State });

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_lesson_mastery_raw_range", "mastery_raw BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_lesson_mastery_effective_range", "mastery_effective BETWEEN 0 AND 100");
        });
    }
}

public class LessonStateEventConfiguration : IEntityTypeConfiguration<LessonStateEvent>
{
    public void Configure(EntityTypeBuilder<LessonStateEvent> b)
    {
        b.Property(x => x.FromState).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ToState).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Reason).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.DetailJson).HasColumnType("jsonb").IsRequired();

        // Màn hình "vì sao khoá" đọc sự kiện mới nhất của cặp user+lesson.
        b.HasIndex(x => new { x.UserId, x.LessonId, x.CreatedAt });
    }
}

public class LessonAttemptConfiguration : IEntityTypeConfiguration<LessonAttempt>
{
    public void Configure(EntityTypeBuilder<LessonAttempt> b)
    {
        b.Property(x => x.DraftStateJson).HasColumnType("jsonb");

        b.HasIndex(x => new { x.UserId, x.LessonId, x.StartedAt });

        // Tìm bài đang làm dở để mở lại đúng chỗ.
        b.HasIndex(x => new { x.UserId, x.SubmittedAt }).HasFilter("submitted_at IS NULL");
    }
}

public class ActivityAttemptConfiguration : IEntityTypeConfiguration<ActivityAttempt>
{
    public void Configure(EntityTypeBuilder<ActivityAttempt> b)
    {
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Skill).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => x.LessonAttemptId);
        b.HasIndex(x => new { x.UserId, x.Skill, x.CreatedAt });

        b.ToTable(t => t.HasCheckConstraint("ck_activity_attempts_score", "score BETWEEN 0 AND 100"));
    }
}

public class PlacementAttemptConfiguration : IEntityTypeConfiguration<PlacementAttempt>
{
    public void Configure(EntityTypeBuilder<PlacementAttempt> b)
    {
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ResultLevel).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ExplanationJson).HasColumnType("jsonb");

        b.Property(x => x.SkillScores)
            .HasConversion(JsonbConverters.SkillScoreMap, JsonbConverters.SkillScoreMapComparer)
            .HasColumnType("jsonb");
        b.UseXminAsConcurrencyToken();

        // Chỉ cho một lượt thi đang mở tại một thời điểm.
        b.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("status = 'InProgress'");

        // Chống thi lại đúng đề vừa làm: truy vấn lượt gần nhất theo form.
        b.HasIndex(x => new { x.UserId, x.FormId, x.SubmittedAt });

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Form).WithMany().HasForeignKey(x => x.FormId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PlacementAnswerConfiguration : IEntityTypeConfiguration<PlacementAnswer>
{
    public void Configure(EntityTypeBuilder<PlacementAnswer> b)
    {
        b.Property(x => x.ResponseJson).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => new { x.AttemptId, x.ItemId }).IsUnique();

        b.HasOne(x => x.Attempt)
            .WithMany(x => x.Answers)
            .HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PlacementSpeakingScoreConfiguration : IEntityTypeConfiguration<PlacementSpeakingScore>
{
    public void Configure(EntityTypeBuilder<PlacementSpeakingScore> b)
    {
        b.Property(x => x.PhonemeIssuesJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.AttemptId, x.ItemId }).IsUnique();

        // Worker chấm nói quét theo cột này.
        b.HasIndex(x => x.Scored).HasFilter("scored = false");
    }
}

public class SpeechAttemptConfiguration : IEntityTypeConfiguration<SpeechAttempt>
{
    public void Configure(EntityTypeBuilder<SpeechAttempt> b)
    {
        b.Property(x => x.ContextType).HasMaxLength(32).IsRequired();
        b.Property(x => x.AudioRelativePath).HasMaxLength(400);
        b.Property(x => x.FeedbackViJson).HasColumnType("jsonb");

        b.HasIndex(x => new { x.UserId, x.CreatedAt });

        // Job dọn dẹp xoá file ghi âm cũ theo cột này.
        b.HasIndex(x => x.CreatedAt).HasFilter("audio_relative_path IS NOT NULL");
    }
}

public class ReviewQueueItemConfiguration : IEntityTypeConfiguration<ReviewQueueItem>
{
    public void Configure(EntityTypeBuilder<ReviewQueueItem> b)
    {
        b.UseXminAsConcurrencyToken();

        b.HasIndex(x => new { x.UserId, x.ItemId }).IsUnique();

        // Truy vấn nóng: câu nào tới hạn ôn của một học viên.
        b.HasIndex(x => new { x.UserId, x.DueAt });

        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_review_queue_interval", "interval_days BETWEEN 1 AND 60");
            t.HasCheckConstraint("ck_review_queue_ease", "ease BETWEEN 1.3 AND 3.0");
        });
    }
}

public class StreakConfiguration : IEntityTypeConfiguration<Streak>
{
    public void Configure(EntityTypeBuilder<Streak> b)
    {
        b.UseXminAsConcurrencyToken();
        b.HasIndex(x => x.UserId).IsUnique();

        b.ToTable(t =>
            t.HasCheckConstraint("ck_streaks_freeze_tokens", "freeze_tokens BETWEEN 0 AND 2"));
    }
}

public class StoryProgressConfiguration : IEntityTypeConfiguration<StoryProgress>
{
    public void Configure(EntityTypeBuilder<StoryProgress> b)
    {
        b.HasIndex(x => new { x.UserId, x.ChapterId }).IsUnique();
        b.HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConsolidationPassConfiguration : IEntityTypeConfiguration<ConsolidationPass>
{
    public void Configure(EntityTypeBuilder<ConsolidationPass> b)
    {
        b.Property(x => x.LessonCodesJson).HasColumnType("jsonb").IsRequired();

        // Mỗi nhóm chỉ qua một lần. Ràng buộc này là thứ giữ cho bộ đếm nhóm không đếm trùng
        // khi hai request nộp cùng lúc.
        b.HasIndex(x => new { x.UserId, x.GroupIndex }).IsUnique();

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WritingAttemptConfiguration : IEntityTypeConfiguration<WritingAttempt>
{
    public void Configure(EntityTypeBuilder<WritingAttempt> b)
    {
        b.Property(x => x.SubmissionJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.FeedbackJson).HasColumnType("jsonb").IsRequired();

        // Tra theo người học và bài, sắp theo lúc nộp: đúng hình dạng của câu hỏi
        // "lần gần nhất mình làm bài này được bao nhiêu".
        b.HasIndex(x => new { x.UserId, x.TaskId, x.SubmittedAt });

        b.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoleplayAttemptConfiguration : IEntityTypeConfiguration<RoleplayAttempt>
{
    public void Configure(EntityTypeBuilder<RoleplayAttempt> b)
    {
        b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.PathJson).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => new { x.UserId, x.ScenarioId, x.StartedAt });

        b.HasOne(x => x.Scenario).WithMany().HasForeignKey(x => x.ScenarioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChallengePassConfiguration : IEntityTypeConfiguration<ChallengePass>
{
    public void Configure(EntityTypeBuilder<ChallengePass> b)
    {
        b.Property(x => x.ItemCodesJson).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => new { x.UserId, x.LessonId }).IsUnique();
    }
}
