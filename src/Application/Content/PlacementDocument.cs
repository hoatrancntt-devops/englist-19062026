using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Content;

/// <summary>
/// Hình dạng của một đề xếp lớp trong content/placement/*.yaml.
///
/// Cùng nguyên tắc với <see cref="LessonDocument"/>: người soạn viết YAML,
/// cổng validate kiểm, seeder nạp. Không ai gõ thẳng vào DB.
/// </summary>
public class PlacementDocument
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Mã form, ví dụ A hoặc B. Khoá tự nhiên mà seeder upsert theo.</summary>
    public string Code { get; set; } = string.Empty;

    public string TitleVi { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; } = 18;
    public bool IsActive { get; set; } = true;

    public List<PlacementItemDocument> Items { get; set; } = [];
}

public class PlacementItemDocument
{
    public string Code { get; set; } = string.Empty;

    public PlacementItemKind Kind { get; set; }

    /// <summary>
    /// Kỹ năng câu này đo.
    ///
    /// Bỏ trống mang hai nghĩa khác nhau, phân biệt bằng <see cref="Kind"/>:
    /// câu Likert bỏ trống vì nó là tự đánh giá, không đo gì; các câu còn lại
    /// bỏ trống thì tính vào trục phụ từ vựng–ngữ pháp.
    /// </summary>
    public SkillType? Skill { get; set; }

    public double Weight { get; set; } = 1.0;

    /// <summary>Độ khó 1-5. Dùng để kiểm đề có trải đủ độ khó hay không.</summary>
    public int Difficulty { get; set; } = 1;

    public int SlowAnswerSeconds { get; set; } = 45;

    /// <summary>
    /// Câu Likert này có tính vào điểm tự đánh giá hay không.
    ///
    /// Chỉ đúng với thang tự tin tăng dần. Câu Likert kiểu "bạn yếu nhất kỹ năng nào"
    /// phải đặt false: các lựa chọn của nó không xếp theo thứ tự cao thấp, quy vị trí
    /// lựa chọn ra điểm sẽ cho ra con số vô nghĩa mà không ai phát hiện.
    /// </summary>
    public bool SelfRating { get; set; } = true;

    public PlacementPromptDocument Prompt { get; set; } = new();

    /// <summary>Bỏ trống với câu Likert và câu nói — hai loại này không có đáp án đúng.</summary>
    public PlacementAnswerDocument? Answer { get; set; }
}

/// <summary>
/// Phần học viên nhìn thấy. Toàn bộ đối tượng này được serialize thẳng vào
/// <c>PlacementFormItem.PromptJson</c> và trả ra client, nên KHÔNG được chứa
/// bất cứ thứ gì gợi ý đáp án.
/// </summary>
public class PlacementPromptDocument
{
    public string InstructionVi { get; set; } = string.Empty;

    /// <summary>Các lựa chọn của Mcq, McqRead và Likert.</summary>
    public List<string>? Choices { get; set; }

    /// <summary>Câu đọc lên cho học viên nghe. Hiện đọc bằng giọng của trình duyệt.</summary>
    public string? AudioText { get; set; }

    /// <summary>Tốc độ đọc, 0.8 cho câu dễ tới 1.2 cho câu khó.</summary>
    public double? Speed { get; set; }

    /// <summary>Đoạn văn của câu đọc hiểu.</summary>
    public string? PassageEn { get; set; }

    public string? QuestionEn { get; set; }

    /// <summary>Câu có chỗ trống (___) hoặc câu có lỗi cần sửa.</summary>
    public string? SentenceEn { get; set; }

    /// <summary>Bối cảnh tiếng Việt của câu viết.</summary>
    public string? ScenarioVi { get; set; }

    /// <summary>Các ý bắt buộc phải có trong email, mô tả bằng tiếng Việt cho học viên đọc.</summary>
    public List<string>? RequiredPointsVi { get; set; }

    /// <summary>Câu học viên phải đọc to. Chỉ dùng cho ReadAloud và Repeat.</summary>
    public string? TargetEn { get; set; }
}

/// <summary>
/// Đáp án và luật chấm. Serialize vào <c>PlacementFormItem.AnswerJson</c> và
/// <b>không bao giờ</b> rời máy chủ.
/// </summary>
public class PlacementAnswerDocument
{
    /// <summary>Vị trí lựa chọn đúng, đếm từ 0. Dùng cho Mcq và McqRead.</summary>
    public int? CorrectIndex { get; set; }

    /// <summary>
    /// Các cách viết được chấp nhận cho câu điền chỗ trống, sửa lỗi, trả lời ngắn.
    ///
    /// Phải liệt kê cả biến thể hợp lệ (viết tắt, đảo trật tự chấp nhận được),
    /// vì bộ chấm so khớp chính xác sau khi chuẩn hoá chứ không đoán ý.
    /// </summary>
    public List<string>? Accepted { get; set; }

    /// <summary>Từ khoá bắt buộc xuất hiện trong email. Mỗi từ khoá là một ý.</summary>
    public List<string>? MustContain { get; set; }

    /// <summary>Số từ tối thiểu. Dưới ngưỡng thì điểm bị nhân theo tỷ lệ thiếu.</summary>
    public int? MinWords { get; set; }

    /// <summary>
    /// Bản sao của <see cref="PlacementItemDocument.SelfRating"/> mà seeder ghi xuống cho câu Likert.
    ///
    /// Nằm ở đây vì đây là luật chấm, và vì luật chấm thì không được rời máy chủ.
    /// Không có nó thì lúc tổng hợp không phân biệt được câu Likert nào là thang tự tin
    /// và câu nào chỉ hỏi kỹ năng yếu nhất — cả hai đều là Likert năm lựa chọn.
    /// </summary>
    public bool? SelfRating { get; set; }
}
