using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Content;

/// <summary>
/// File audio sinh sẵn lúc seed bằng Piper. Sinh trước nghĩa là chi phí runtime phần nghe bằng 0.
/// </summary>
public class MediaAsset : Entity
{
    /// <summary>Khoá tự nhiên: hash của (text + voice + speed). Trùng thì không sinh lại.</summary>
    public required string ContentHash { get; set; }

    /// <summary>Đường dẫn tương đối trong volume media, ví dụ audio/INF-03/ls-1.wav.</summary>
    public required string RelativePath { get; set; }

    public required string MimeType { get; set; }
    public long SizeBytes { get; set; }
    public int DurationMs { get; set; }

    /// <summary>Văn bản gốc dùng để sinh. Giữ lại để tái tạo khi đổi giọng đọc.</summary>
    public required string SourceText { get; set; }

    public required string Voice { get; set; }

    /// <summary>Tốc độ đọc. Bậc thấp đọc chậm hơn: L1 0.85 đến L4 1.3.</summary>
    public double Speed { get; set; } = 1.0;
}

/// <summary>
/// Lịch sử phiên bản nội dung, ghi lại mỗi lần seeder nạp file YAML đổi nội dung.
/// Đây là dữ liệu cho diff viewer trong admin.
/// </summary>
public class ContentVersion : Entity
{
    /// <summary>Loại đối tượng: lesson, placement_form, roleplay, story, writing_set.</summary>
    public required string EntityType { get; set; }

    public required string EntityCode { get; set; }

    public int VersionNumber { get; set; }

    public required string SourceHash { get; set; }

    /// <summary>Ảnh chụp toàn bộ nội dung tại thời điểm nạp, dạng JSONB.</summary>
    public required string SnapshotJson { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    public Guid? PublishedByUserId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Ghi chú thay đổi. Người biên soạn tự điền, không sinh tự động.</summary>
    public string? ChangeNote { get; set; }
}

/// <summary>
/// Chương truyện. Cả lộ trình là sáu tháng đầu đi làm ở một công ty,
/// mỗi phase học là một chương.
/// </summary>
public class StoryChapter : Entity
{
    public required string Code { get; set; }
    public int Number { get; set; }
    public required string TitleVi { get; set; }

    /// <summary>Câu mở chương, một dòng, có sức kéo.</summary>
    public required string HookVi { get; set; }

    public required string BodyVi { get; set; }
    public required string EndsVi { get; set; }

    /// <summary>Track mà chương này gắn vào.</summary>
    public LearningTrack Track { get; set; }

    /// <summary>Nhân vật xuất hiện lần đầu ở chương này, JSONB mảng tên.</summary>
    public required string NewCharactersJson { get; set; }
}

/// <summary>Kịch bản đóng vai. Chạy hoàn toàn bằng graph, không cần AI.</summary>
public class RoleplayScenario : Entity, ISoftDelete
{
    public required string Code { get; set; }
    public required string TitleVi { get; set; }
    public required string ContextVi { get; set; }

    public LearningTrack Track { get; set; }
    public CefrLevel Level { get; set; }

    /// <summary>Tên nhân vật đối thoại, ví dụ "Vendor support engineer".</summary>
    public required string PartnerName { get; set; }

    /// <summary>Mã node bắt đầu.</summary>
    public required string StartNodeCode { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Hash của file YAML nguồn. Seeder so hash để biết có phải dựng lại không.</summary>
    public string SourceHash { get; set; } = string.Empty;

    public ICollection<RoleplayNode> Nodes { get; set; } = [];
}

/// <summary>
/// Một lượt trong kịch bản: đối phương nói, học viên chọn đáp.
/// Mỗi lựa chọn trỏ tới node kế, tạo thành cây phân nhánh.
/// </summary>
public class RoleplayNode : Entity
{
    public Guid ScenarioId { get; set; }
    public RoleplayScenario? Scenario { get; set; }

    public required string Code { get; set; }

    /// <summary>Lời của nhân vật đối thoại.</summary>
    public required string PartnerLineEn { get; set; }
    public required string PartnerLineVi { get; set; }

    /// <summary>Audio sinh sẵn cho lời trên.</summary>
    public Guid? AudioAssetId { get; set; }

    /// <summary>
    /// Các lựa chọn trả lời, JSONB mảng:
    /// [{"en":"...","vi":"...","next":"n3","quality":"good|curt|wrong","hintVi":"..."}]
    /// </summary>
    public required string ChoicesJson { get; set; }

    public bool IsTerminal { get; set; }

    /// <summary>
    /// Kết thúc này có tính là hoàn thành tốt không. Chỉ có nghĩa khi <see cref="IsTerminal"/>.
    ///
    /// Một kịch bản có nhiều đường kết thúc, và không phải đường nào cũng là thất bại:
    /// học viên đi lối cộc lốc vẫn xong việc, chỉ kém lịch sự. Cờ này phân biệt hai loại đó.
    /// </summary>
    public bool IsSuccessEnding { get; set; }

    /// <summary>Tóm tắt hiển thị khi kết thúc ở node này.</summary>
    public string? SummaryVi { get; set; }
}
