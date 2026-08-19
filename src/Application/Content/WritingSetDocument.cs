using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Content;

/// <summary>
/// Một bộ bài luyện viết đọc từ YAML.
///
/// Khác bước viết nằm trong bài học ở chỗ đây là bộ drill độc lập: học viên vào làm riêng,
/// không cần mở bài nào. Giống ở chỗ quan trọng nhất — dùng đúng khuôn trường và đúng bộ chấm
/// của bước viết trong bài học, nên người soạn chỉ phải học một hình dạng, và một bài viết
/// được chấm y hệt nhau dù nó nằm trong bài học hay trong bộ drill.
/// </summary>
public class WritingSetDocument
{
    public string Code { get; set; } = string.Empty;
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Bối cảnh chung: học viên đang viết cho ai, trong tình huống nào.</summary>
    public string ContextVi { get; set; } = string.Empty;

    public LearningTrack Track { get; set; }
    public CefrLevel Level { get; set; }

    public List<WritingSetTaskDocument> Tasks { get; set; } = [];
}

/// <summary>
/// Một bài trong bộ.
///
/// Kế thừa nguyên khuôn bước viết của bài học (<see cref="WritingDocument"/>) và chỉ thêm
/// ba thứ mà bộ drill cần: mã bài, ngưỡng đạt riêng, và thứ tự hiển thị cho dạng sắp xếp.
/// </summary>
public class WritingSetTaskDocument : WritingDocument
{
    public string Code { get; set; } = string.Empty;

    /// <summary>Điểm tối thiểu coi là đạt. Mặc định khớp mặc định của cột trong DB.</summary>
    public int PassScore { get; set; } = 80;

    /// <summary>
    /// Dạng Reorder: các mảnh theo thứ tự HIỂN THỊ, do người soạn xáo sẵn.
    ///
    /// Xáo trong file chứ không xáo lúc chạy vì hai lý do: thứ tự ổn định giữa các lần tải,
    /// và người soạn nhìn thấy được mình vừa tạo ra một phép xáo dễ đoán hay không.
    /// </summary>
    public List<string> Fragments { get; set; } = [];
}
