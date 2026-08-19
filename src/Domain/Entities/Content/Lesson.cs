using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Content;

/// <summary>
/// Một bài học. Nguồn sự thật là file YAML trong content/; bảng này là bản đã nạp.
/// Seeder upsert theo <see cref="Code"/> nên nạp lại không đụng tới tiến độ học viên.
/// </summary>
/// <remarks>
/// Cố ý KHÔNG mang concurrency token. Bảng nội dung chỉ có đúng một người ghi là seeder,
/// chạy tuần tự lúc khởi động. Token chỉ nên đặt ở bảng thật sự có nhiều người ghi
/// đồng thời (tiến độ học, hàng đợi ôn tập, hộp thư đi) — đặt bừa thì mỗi lần seed
/// lưu hai lượt sẽ tự đụng chính mình.
/// </remarks>
public class Lesson : Entity, ISoftDelete
{
    /// <summary>Mã ổn định, ví dụ INF-03. Đây là khoá tự nhiên dùng ở mọi nơi ngoài DB.</summary>
    public required string Code { get; set; }

    public required string Slug { get; set; }
    public required string TitleVi { get; set; }
    public required string TitleEn { get; set; }

    public LearningTrack Track { get; set; }
    public CefrLevel Level { get; set; }

    /// <summary>
    /// Tầng ngữ cảnh: đời sống, văn phòng, hay chuyên môn.
    /// Lộ trình của người mất gốc đi Life trước, không nhảy thẳng vào Professional.
    /// </summary>
    public ContextLayer Layer { get; set; } = ContextLayer.Life;

    /// <summary>
    /// Các kỹ năng mà bài này thực sự dạy được. Bài chỉ có văn bản thì không có Listening.
    /// Chế độ học một kỹ năng dùng cột này để lọc, tránh mở bài rồi mới thấy trống.
    /// </summary>
    public List<SkillType> SupportedSkills { get; set; } = [];

    /// <summary>Cụm bài cùng chủ đề, ví dụ U-INF1. Dùng để gom nhóm trên roadmap.</summary>
    public string? UnitCode { get; set; }

    /// <summary>Thứ tự trong track. Không quyết định mở khoá — DAG mới quyết định.</summary>
    public int OrderIndex { get; set; }

    public int EstimatedMinutes { get; set; }

    /// <summary>Bài checkpoint chốt một phase, gộp nhiều bài trước đó.</summary>
    public bool IsCheckpoint { get; set; }

    /// <summary>Khoá hình minh hoạ. Giao diện tra khoá này ra một component SVG nhúng sẵn.</summary>
    public string? Illustration { get; set; }

    public required string ObjectiveVi { get; set; }

    /// <summary>Tiêu chí đạt, phải đo được. Cổng validate chặn nếu để trống.</summary>
    public required string ObjectiveObservable { get; set; }

    /// <summary>
    /// Trọng số từng kỹ năng khi tính mastery, tổng phải bằng 1.0.
    /// Lưu JSONB dạng {"Listening":0.3,...}.
    /// </summary>
    public Dictionary<SkillType, double> MasteryWeights { get; set; } = [];

    /// <summary>Giải thích tiếng Việt vì sao khó và cách khắc phục. Bắt buộc, cổng validate chặn.</summary>
    public required string ExplanationJson { get; set; }

    /// <summary>Lỗi hay gặp, tối thiểu 2. Cổng validate chặn nếu ít hơn.</summary>
    public required string CommonMistakesJson { get; set; }

    /// <summary>Toàn bộ nội dung bài (hội thoại, từ vựng, mẫu câu, quiz) dạng JSONB.</summary>
    public required string BodyJson { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    /// <summary>Hash SHA-256 của file YAML nguồn. Seeder bỏ qua bài không đổi.</summary>
    public required string SourceHash { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<LessonPrerequisite> Prerequisites { get; set; } = [];
    public ICollection<LessonActivity> Activities { get; set; } = [];
}

/// <summary>Cạnh của DAG tiên quyết. Bất biến: không đi từ bậc cao xuống bậc thấp.</summary>
public class LessonPrerequisite : Entity
{
    public Guid LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    /// <summary>Bài phải học trước.</summary>
    public Guid RequiredLessonId { get; set; }
    public Lesson? RequiredLesson { get; set; }

    public PrerequisiteKind Kind { get; set; } = PrerequisiteKind.Hard;

    /// <summary>Mastery tối thiểu của bài tiên quyết, thang 0-100.</summary>
    public int MinMastery { get; set; } = 60;
}

/// <summary>Một bước trong lesson player. Thứ tự cố định, mỗi bước gắn đúng một kỹ năng.</summary>
public class LessonActivity : Entity
{
    public Guid LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public ActivityKind Kind { get; set; }
    public SkillType Skill { get; set; }
    public int OrderIndex { get; set; }

    /// <summary>Nội dung riêng của bước, dạng JSONB. Hình dạng phụ thuộc Kind.</summary>
    public required string PayloadJson { get; set; }

    /// <summary>Điểm tối thiểu để bước này tính là đạt, thang 0-100.</summary>
    public int PassScore { get; set; } = 80;

    public ICollection<LessonItem> Items { get; set; } = [];
}

/// <summary>
/// Câu hỏi hoặc drill đơn lẻ. Tách khỏi activity vì ôn tập giãn cách
/// xếp lịch theo từng item, không theo cả bước.
/// </summary>
public class LessonItem : Entity
{
    public Guid ActivityId { get; set; }
    public LessonActivity? Activity { get; set; }

    /// <summary>Mã ổn định trong phạm vi bài, ví dụ INF-03-Q2.</summary>
    public required string Code { get; set; }

    public int OrderIndex { get; set; }

    /// <summary>Đề bài dạng JSONB.</summary>
    public required string PromptJson { get; set; }

    /// <summary>
    /// Đáp án dạng JSONB. KHÔNG BAO GIỜ đưa cột này vào DTO trả về client —
    /// chấm điểm chạy hoàn toàn phía server.
    /// </summary>
    public required string AnswerJson { get; set; }

    public int Difficulty { get; set; } = 1;
}
