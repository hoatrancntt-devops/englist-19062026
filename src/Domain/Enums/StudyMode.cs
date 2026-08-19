namespace EnglishForIT.Domain.Enums;

/// <summary>
/// Chế độ học do học viên chọn. Quyết định lộ trình lấy bước nào của bài và bỏ bước nào.
///
/// Thứ tự ưu tiên kỹ năng toàn hệ thống: Nghe, Nói, Đọc, Viết.
/// Thứ tự này áp cho cả chế độ hỗn hợp lẫn thứ tự các bước bên trong một bài.
/// </summary>
public enum StudyMode
{
    /// <summary>Đủ bốn kỹ năng theo thứ tự nghe, nói, đọc, viết. Mặc định.</summary>
    Mixed = 0,

    /// <summary>Chỉ nghe. Lộ trình chỉ lấy bước Listen và phần nghe của quiz.</summary>
    ListeningOnly = 1,

    /// <summary>Chỉ nói. Lấy bước Shadow và Speak, cần micro nên onboarding phải qua test micro.</summary>
    SpeakingOnly = 2,

    /// <summary>Chỉ đọc. Lấy bước Read và câu hỏi đọc hiểu. Không cần loa cũng học được.</summary>
    ReadingOnly = 3,

    /// <summary>Chỉ viết. Lấy bước Write, chấm bằng luật tại server.</summary>
    WritingOnly = 4
}

/// <summary>
/// Tầng ngữ cảnh của nội dung. Một học viên đi từ tầng Life lên tầng Professional,
/// nhưng có thể chọn học lệch nếu công việc đòi hỏi.
/// </summary>
public enum ContextLayer
{
    /// <summary>Giao tiếp đời sống: chào hỏi, số, giờ, ăn uống, đi lại, hỏi đường.</summary>
    Life = 0,

    /// <summary>Đời sống văn phòng: pantry, xin nghỉ, hỏi việc, nhắn tin nội bộ, họp ngắn.</summary>
    Office = 1,

    /// <summary>Chuyên môn: vận hành, báo cáo, họp team, xử lý sự cố, viết report.</summary>
    Professional = 2
}
