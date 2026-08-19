using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Content;

/// <summary>
/// Đề xếp lớp. Có nhiều form song song để người thi lại không gặp đúng đề cũ.
/// </summary>
public class PlacementForm : Entity
{
    /// <summary>Mã form, ví dụ A hoặc B.</summary>
    public required string Code { get; set; }

    public required string TitleVi { get; set; }
    public int EstimatedMinutes { get; set; } = 18;
    public bool IsActive { get; set; } = true;

    public required string SourceHash { get; set; }

    public ICollection<PlacementFormItem> Items { get; set; } = [];
}

/// <summary>
/// Một câu trong đề. Cột đáp án không bao giờ được map ra DTO client —
/// đây là ràng buộc kiến trúc, không phải khuyến nghị.
/// </summary>
public class PlacementFormItem : Entity
{
    public Guid FormId { get; set; }
    public PlacementForm? Form { get; set; }

    public required string Code { get; set; }
    public int OrderIndex { get; set; }

    public PlacementItemKind Kind { get; set; }

    /// <summary>Kỹ năng câu này đo. Câu Likert tự đánh giá thì để null.</summary>
    public SkillType? Skill { get; set; }

    /// <summary>Phần hiển thị cho học viên, JSONB. An toàn để trả ra client.</summary>
    public required string PromptJson { get; set; }

    /// <summary>Đáp án đúng, JSONB. KHÔNG rời server trong bất kỳ hoàn cảnh nào.</summary>
    public required string AnswerJson { get; set; }

    /// <summary>Trọng số khi tính điểm trục kỹ năng.</summary>
    public double Weight { get; set; } = 1.0;

    public int Difficulty { get; set; } = 1;

    /// <summary>Ngưỡng giây coi là trả lời quá chậm, dùng cho chỉ số đoán mò.</summary>
    public int SlowAnswerSeconds { get; set; } = 45;
}

/// <summary>Bộ bài luyện viết, chấm bằng luật ngay tại server.</summary>
public class WritingSet : Entity
{
    public required string Code { get; set; }
    public required string TitleVi { get; set; }
    public required string ContextVi { get; set; }

    public LearningTrack Track { get; set; }
    public CefrLevel Level { get; set; }

    public required string SourceHash { get; set; }

    public ICollection<WritingTask> Tasks { get; set; } = [];
}

public class WritingTask : Entity
{
    public Guid SetId { get; set; }
    public WritingSet? Set { get; set; }

    public required string Code { get; set; }
    public int OrderIndex { get; set; }

    public WritingTaskKind Kind { get; set; }

    public required string PromptJson { get; set; }

    /// <summary>Đáp án và luật chấm, JSONB. Giữ phía server.</summary>
    public required string RubricJson { get; set; }

    public int PassScore { get; set; } = 80;
}
