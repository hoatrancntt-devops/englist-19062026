namespace EnglishForIT.Domain.Enums;

/// <summary>Vai trò hệ thống. Lưu dạng chuỗi trong DB để đọc log không phải tra bảng.</summary>
public enum UserRole
{
    Learner = 0,
    ContentEditor = 1,
    Support = 2,
    Admin = 3,
    SuperAdmin = 4
}

/// <summary>Bốn bậc của thang nội bộ: L1=PreA1, L2=A1, L3=A2, L4=B1.</summary>
public enum CefrLevel
{
    PreA1 = 0,
    A1 = 1,
    A2 = 2,
    B1 = 3
}

public enum SkillType
{
    Listening = 0,
    Speaking = 1,
    Reading = 2,
    Writing = 3
}

/// <summary>
/// Nhánh nghề. Điều kiện mở khoá vẫn do CefrLevel cộng DAG tiên quyết quyết định —
/// enum này không mở khoá gì cả.
///
/// Nhưng THỨ TỰ KHAI BÁO ở đây chính là thứ tự hiển thị trên lộ trình, và nó cố ý
/// khớp thứ tự soạn nội dung: Helpdesk trước Infrastructure, vì đó là nơi kỹ sư
/// mất gốc buộc phải nói tiếng Anh sớm nhất. Không sắp theo nhánh thì hai nhánh
/// cùng bậc sẽ xen kẽ nhau trên lộ trình và học viên không biết mình đang đi nhánh nào.
///
/// Giá trị số đổi được tự do: cột lưu dạng chuỗi nên không cần migration.
/// </summary>
public enum LearningTrack
{
    Foundation = 0,
    Helpdesk = 1,
    Infrastructure = 2,
    Security = 3,
    Cloud = 4,
    Ai = 5,
    Reading = 6
}

public enum LessonState
{
    Locked = 0,
    Previewable = 1,
    Available = 2,
    InProgress = 3,
    Mastered = 4,
    NeedsReview = 5
}

public enum PrerequisiteKind
{
    /// <summary>Chặn thật: chưa đạt thì không mở được bài.</summary>
    Hard = 0,

    /// <summary>Chỉ cảnh báo: hiện nhắc nhở nhưng vẫn cho học.</summary>
    Soft = 1
}

public enum ActivityKind
{
    Listen = 0,
    Shadow = 1,
    Speak = 2,
    Vocab = 3,
    Read = 4,
    Write = 5,
    Quiz = 6
}

public enum ContentStatus
{
    Draft = 0,
    InReview = 1,
    Published = 2,
    Archived = 3
}
