using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Content;

/// <summary>
/// Một kịch bản roleplay đọc từ YAML.
///
/// Kịch bản là đồ thị có hướng: mỗi node là một lượt đối phương nói, mỗi lựa chọn dẫn tới
/// node kế. Khác bài học ở chỗ học viên đi theo nhánh khác nhau tuỳ lựa chọn, nên cổng chất
/// lượng phải kiểm được cả tính liên thông của đồ thị chứ không chỉ từng node.
/// </summary>
public class RoleplayDocument
{
    public string Code { get; set; } = string.Empty;
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Bối cảnh hiện trước khi bắt đầu: học viên là ai, đang cần gì.</summary>
    public string ContextVi { get; set; } = string.Empty;

    public LearningTrack Track { get; set; }
    public CefrLevel Level { get; set; }

    /// <summary>Tên và vai của nhân vật đối thoại, ví dụ "Mai — vendor support engineer".</summary>
    public string PartnerName { get; set; } = string.Empty;

    public string StartNode { get; set; } = string.Empty;

    public List<RoleplayNodeDocument> Nodes { get; set; } = [];
}

public class RoleplayNodeDocument
{
    public string Code { get; set; } = string.Empty;

    public string PartnerLineEn { get; set; } = string.Empty;
    public string PartnerLineVi { get; set; } = string.Empty;

    public List<RoleplayChoiceDocument> Choices { get; set; } = [];

    /// <summary>Node kết thúc: không có lựa chọn, chỉ có lời tổng kết.</summary>
    public bool Terminal { get; set; }

    public string? SummaryVi { get; set; }

    /// <summary>Kết thúc này có tính là hoàn thành tốt không. Chỉ dùng khi Terminal.</summary>
    public bool Success { get; set; }
}

public class RoleplayChoiceDocument
{
    public string En { get; set; } = string.Empty;
    public string Vi { get; set; } = string.Empty;

    /// <summary>Mã node đi tới. Rỗng nghĩa là kết thúc ngay tại đây.</summary>
    public string? Next { get; set; }

    /// <summary>
    /// Chất lượng câu trả lời: good, curt, wrong.
    ///
    /// "curt" là câu đúng ngữ pháp nhưng cộc lốc — đây là lỗi phổ biến nhất của kỹ sư
    /// Việt Nam nói tiếng Anh với đối tác, và là lý do chính khiến roleplay tồn tại.
    /// </summary>
    public string Quality { get; set; } = "good";

    /// <summary>Giải thích hiện sau khi chọn. Bắt buộc với lựa chọn không phải "good".</summary>
    public string? FeedbackVi { get; set; }
}
