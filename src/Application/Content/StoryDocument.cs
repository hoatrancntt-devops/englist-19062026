using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Content;

/// <summary>
/// Một chương truyện đọc từ YAML.
///
/// Truyện là mạch xuyên suốt khoá học: sáu tháng đầu đi làm của học viên ở HT Group.
/// Chương không phải bài học — không có bước, không chấm điểm, không tính vào mastery.
/// Nó tồn tại để trả lời câu "học tiếp làm gì" ở đúng lúc học viên bắt đầu chán.
///
/// Khác roleplay ở chỗ chương chỉ mở chứ không tương tác: mốc mở là một mã bài, và
/// thứ tự đọc do <see cref="Number"/> quyết định chứ không do người học chọn.
/// </summary>
public class StoryDocument
{
    public string Code { get; set; } = string.Empty;

    /// <summary>Số thứ tự đọc, duy nhất trên toàn bộ truyện.</summary>
    public int Number { get; set; }

    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Track mà chương này gắn vào. Nhiều chương dùng chung một track là hợp lệ.</summary>
    public LearningTrack Track { get; set; }

    /// <summary>Mã bài phải thông thạo thì chương mới mở, ví dụ <c>LIFE-04</c>.</summary>
    public string UnlockAfterLesson { get; set; } = string.Empty;

    /// <summary>Câu mở chương, một dòng, có sức kéo. Hiện ở danh sách chương khi chương còn khoá.</summary>
    public string HookVi { get; set; } = string.Empty;

    public string BodyVi { get; set; } = string.Empty;

    /// <summary>Câu kết chương, nối sang việc học sắp tới.</summary>
    public string EndsVi { get; set; } = string.Empty;

    /// <summary>Nhân vật xuất hiện lần đầu ở chương này. Tên kèm vai, ví dụ "Mai — kỹ sư cùng nhóm".</summary>
    public List<string> NewCharacters { get; set; } = [];
}
