using EnglishForIT.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishForIT.Infrastructure.Persistence.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        b.Property(x => x.TitleVi).HasMaxLength(300).IsRequired();
        b.Property(x => x.TitleEn).HasMaxLength(300).IsRequired();
        b.Property(x => x.UnitCode).HasMaxLength(32);
        b.Property(x => x.Illustration).HasMaxLength(48);
        b.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();

        b.Property(x => x.Track).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Layer).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

        b.Property(x => x.SupportedSkills)
            .HasConversion(JsonbConverters.SkillList, JsonbConverters.SkillListComparer)
            .HasColumnType("jsonb");

        b.Property(x => x.MasteryWeights)
            .HasConversion(JsonbConverters.SkillScoreMap, JsonbConverters.SkillScoreMapComparer)
            .HasColumnType("jsonb");

        b.Property(x => x.ExplanationJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.CommonMistakesJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.BodyJson).HasColumnType("jsonb").IsRequired();

        // Code là khoá tự nhiên mà seeder upsert theo. Unique ở đây là thứ bảo đảm
        // nạp lại nội dung không tạo bản trùng và không đụng tới tiến độ học viên.
        b.HasIndex(x => x.Code).IsUnique().HasFilter("deleted_at IS NULL");
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("deleted_at IS NULL");

        // Truy vấn roadmap: lọc theo tầng + bậc + track rồi sắp theo thứ tự.
        b.HasIndex(x => new { x.Layer, x.Level, x.Track, x.OrderIndex });

        b.HasQueryFilter(x => x.DeletedAt == null);

        b.ToTable(t =>
        {
            // Trần 12 phút là quy tắc sản phẩm, không phải khuyến nghị: bài dài hơn thì
            // học viên bỏ giữa chừng. Chốt bằng ràng buộc DB để không ai lách qua seeder.
            t.HasCheckConstraint("ck_lessons_est_minutes", "estimated_minutes BETWEEN 3 AND 12");
        });
    }
}

public class LessonPrerequisiteConfiguration : IEntityTypeConfiguration<LessonPrerequisite>
{
    public void Configure(EntityTypeBuilder<LessonPrerequisite> b)
    {
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);

        b.HasIndex(x => new { x.LessonId, x.RequiredLessonId }).IsUnique();

        // Truy vấn ngược: "bài nào phụ thuộc bài này" — dùng khi tính lại downstream.
        b.HasIndex(x => x.RequiredLessonId);

        b.HasOne(x => x.Lesson)
            .WithMany(x => x.Prerequisites)
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Không cascade phía này: xoá một bài không được âm thầm phá DAG của bài khác.
        b.HasOne(x => x.RequiredLesson)
            .WithMany()
            .HasForeignKey(x => x.RequiredLessonId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_lesson_prerequisites_no_self",
                "lesson_id <> required_lesson_id");
            t.HasCheckConstraint("ck_lesson_prerequisites_min_mastery",
                "min_mastery BETWEEN 0 AND 100");
        });
    }
}

public class LessonActivityConfiguration : IEntityTypeConfiguration<LessonActivity>
{
    public void Configure(EntityTypeBuilder<LessonActivity> b)
    {
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Skill).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => new { x.LessonId, x.OrderIndex }).IsUnique();

        b.HasOne(x => x.Lesson)
            .WithMany(x => x.Activities)
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        b.ToTable(t =>
            t.HasCheckConstraint("ck_lesson_activities_pass_score", "pass_score BETWEEN 0 AND 100"));
    }
}

public class LessonItemConfiguration : IEntityTypeConfiguration<LessonItem>
{
    public void Configure(EntityTypeBuilder<LessonItem> b)
    {
        b.Property(x => x.Code).HasMaxLength(48).IsRequired();
        b.Property(x => x.PromptJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.AnswerJson).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.ActivityId, x.OrderIndex });

        b.HasOne(x => x.Activity)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> b)
    {
        b.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.RelativePath).HasMaxLength(400).IsRequired();
        b.Property(x => x.MimeType).HasMaxLength(64).IsRequired();
        b.Property(x => x.Voice).HasMaxLength(64).IsRequired();

        // Trùng hash thì tái dùng file cũ, không sinh lại. Đây là thứ giữ seed ở mức vài phút.
        b.HasIndex(x => x.ContentHash).IsUnique();
    }
}

public class ContentVersionConfiguration : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(32).IsRequired();
        b.Property(x => x.EntityCode).HasMaxLength(48).IsRequired();
        b.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.SnapshotJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ChangeNote).HasMaxLength(1000);

        b.HasIndex(x => new { x.EntityType, x.EntityCode, x.VersionNumber }).IsUnique();
    }
}

public class StoryChapterConfiguration : IEntityTypeConfiguration<StoryChapter>
{
    public void Configure(EntityTypeBuilder<StoryChapter> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.TitleVi).HasMaxLength(300).IsRequired();
        b.Property(x => x.HookVi).HasMaxLength(500).IsRequired();
        b.Property(x => x.Track).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.NewCharactersJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.UnlockAfterLessonCode).HasMaxLength(32).IsRequired();
        b.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();

        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Number).IsUnique();

        // Mốc mở chương tra theo mã bài mỗi lần dựng danh sách chương cho một học viên.
        b.HasIndex(x => x.UnlockAfterLessonCode);
    }
}

public class RoleplayScenarioConfiguration : IEntityTypeConfiguration<RoleplayScenario>
{
    public void Configure(EntityTypeBuilder<RoleplayScenario> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.TitleVi).HasMaxLength(300).IsRequired();
        b.Property(x => x.PartnerName).HasMaxLength(120).IsRequired();
        b.Property(x => x.StartNodeCode).HasMaxLength(32).IsRequired();
        b.Property(x => x.Track).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

        b.HasIndex(x => x.Code).IsUnique().HasFilter("deleted_at IS NULL");
        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public class RoleplayNodeConfiguration : IEntityTypeConfiguration<RoleplayNode>
{
    public void Configure(EntityTypeBuilder<RoleplayNode> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.ChoicesJson).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => new { x.ScenarioId, x.Code }).IsUnique();

        b.HasOne(x => x.Scenario)
            .WithMany(x => x.Nodes)
            .HasForeignKey(x => x.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlacementFormConfiguration : IEntityTypeConfiguration<PlacementForm>
{
    public void Configure(EntityTypeBuilder<PlacementForm> b)
    {
        b.Property(x => x.Code).HasMaxLength(16).IsRequired();
        b.Property(x => x.TitleVi).HasMaxLength(300).IsRequired();
        b.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();

        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class PlacementFormItemConfiguration : IEntityTypeConfiguration<PlacementFormItem>
{
    public void Configure(EntityTypeBuilder<PlacementFormItem> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Skill).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.PromptJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.AnswerJson).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => new { x.FormId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.FormId, x.OrderIndex }).IsUnique();

        b.HasOne(x => x.Form)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.FormId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VocabDeckConfiguration : IEntityTypeConfiguration<VocabDeck>
{
    public void Configure(EntityTypeBuilder<VocabDeck> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.TitleVi).HasMaxLength(300).IsRequired();
        b.Property(x => x.ContextVi).HasMaxLength(2000).IsRequired();
        b.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();

        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Band);
    }
}

public class VocabWordConfiguration : IEntityTypeConfiguration<VocabWord>
{
    public void Configure(EntityTypeBuilder<VocabWord> b)
    {
        b.Property(x => x.Term).HasMaxLength(120).IsRequired();
        b.Property(x => x.Ipa).HasMaxLength(160).IsRequired();
        b.Property(x => x.MeaningVi).HasMaxLength(500).IsRequired();
        b.Property(x => x.Chunk).HasMaxLength(500).IsRequired();
        b.Property(x => x.Emoji).HasMaxLength(32);
        b.Property(x => x.MnemonicVi).HasMaxLength(1000);

        b.HasOne(x => x.Deck)
            .WithMany(d => d.Words)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

        // Một từ chỉ xuất hiện một lần trong một bộ. Cổng nội dung đã chặn, ràng buộc này là
        // lớp thứ hai để một lần seed lỗi không âm thầm nhân đôi từ.
        b.HasIndex(x => new { x.DeckId, x.Term }).IsUnique();
    }
}

public class VocabWordProgressConfiguration : IEntityTypeConfiguration<VocabWordProgress>
{
    public void Configure(EntityTypeBuilder<VocabWordProgress> b)
    {
        b.HasIndex(x => new { x.UserId, x.WordId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.DueAt });

        // Xoá học viên là xoá sạch tiến độ của họ. Thiếu dòng này thì bảng đầy dần những dòng
        // mồ côi trỏ về tài khoản không còn tồn tại — đúng lỗi đã gặp ở story và writing.
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Word).WithMany().HasForeignKey(x => x.WordId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WritingSetConfiguration : IEntityTypeConfiguration<WritingSet>
{
    public void Configure(EntityTypeBuilder<WritingSet> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.TitleVi).HasMaxLength(300).IsRequired();
        b.Property(x => x.Track).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();

        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class WritingTaskConfiguration : IEntityTypeConfiguration<WritingTask>
{
    public void Configure(EntityTypeBuilder<WritingTask> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.PromptJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.RubricJson).HasColumnType("jsonb").IsRequired();

        b.HasIndex(x => new { x.SetId, x.Code }).IsUnique();

        b.HasOne(x => x.Set)
            .WithMany(x => x.Tasks)
            .HasForeignKey(x => x.SetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
