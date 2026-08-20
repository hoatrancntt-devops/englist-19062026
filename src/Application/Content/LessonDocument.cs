using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Content;

/// <summary>
/// Hình dạng của một file bài học trong content/lessons/**.yaml.
///
/// Đây là hợp đồng giữa người soạn nội dung và hệ thống. Người soạn viết YAML,
/// cổng validate kiểm, seeder nạp. Không ai gõ thẳng vào DB.
/// </summary>
public class LessonDocument
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Mã ổn định, ví dụ LIFE-03. Đây là khoá tự nhiên mà seeder upsert theo.</summary>
    public string Code { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
    public string TitleVi { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;

    public ContextLayer Layer { get; set; }
    public LearningTrack Track { get; set; }
    public CefrLevel Level { get; set; }

    public string? UnitCode { get; set; }
    public int OrderIndex { get; set; }
    public int EstimatedMinutes { get; set; } = 11;
    public bool IsCheckpoint { get; set; }

    /// <summary>
    /// Khoá hình minh hoạ, chọn từ <see cref="IllustrationCatalogue"/>.
    ///
    /// Hình vẽ bằng SVG nhúng thẳng trong giao diện, không tải ảnh từ ngoài:
    /// vừa không phụ thuộc CDN, vừa tự đổi màu theo nền sáng/tối.
    /// Bỏ trống thì bài không có hình — hợp lệ, chỉ là kém sinh động hơn.
    /// </summary>
    public string? Illustration { get; set; }

    public string ObjectiveVi { get; set; } = string.Empty;

    /// <summary>Tiêu chí đạt, phải đo được. Cổng validate chặn nếu chung chung.</summary>
    public string ObjectiveObservable { get; set; } = string.Empty;

    /// <summary>Trọng số từng kỹ năng khi tính mastery. Tổng phải bằng 1.0.</summary>
    public Dictionary<SkillType, double> MasteryWeights { get; set; } = [];

    public List<PrerequisiteDocument> Prerequisites { get; set; } = [];

    public List<VocabularyDocument> Vocabulary { get; set; } = [];
    public List<SentencePatternDocument> SentencePatterns { get; set; } = [];

    public DialogueDocument? Dialogue { get; set; }
    public ListeningDocument? Listening { get; set; }
    public List<SpeakingDrillDocument> SpeakingDrills { get; set; } = [];
    public ReadingDocument? Reading { get; set; }
    public WritingDocument? Writing { get; set; }
    public List<QuizItemDocument> Quiz { get; set; } = [];

    public ExplanationDocument Explanation { get; set; } = new();
    public List<CommonMistakeDocument> CommonMistakes { get; set; } = [];

    public string? MemoryTrickVi { get; set; }

    /// <summary>
    /// Kỹ năng bài này thực sự dạy được. Bỏ trống thì seeder tự suy ra từ các phần có mặt —
    /// suy ra tự động an toàn hơn khai tay, vì khai tay hay quên cập nhật khi sửa bài.
    /// </summary>
    public List<SkillType> SupportedSkills { get; set; } = [];
}

public class PrerequisiteDocument
{
    /// <summary>Mã bài phải học trước.</summary>
    public string Lesson { get; set; } = string.Empty;

    public int MinMastery { get; set; } = 60;
    public PrerequisiteKind Kind { get; set; } = PrerequisiteKind.Hard;
}

public class VocabularyDocument
{
    public string Term { get; set; } = string.Empty;
    public string Ipa { get; set; } = string.Empty;
    public string MeaningVi { get; set; } = string.Empty;

    /// <summary>Cụm dùng được ngay, không phải câu ví dụ chung chung.</summary>
    public string Chunk { get; set; } = string.Empty;

    /// <summary>
    /// Một hoặc hai emoji gợi nghĩa, hiện cạnh từ.
    ///
    /// Có cơ sở chứ không phải trang trí: thuyết mã kép cho rằng kênh hình và kênh chữ được
    /// xử lý độc lập, nên từ được mã hoá cả hai đường thì nhớ lâu hơn hẳn. Từ trừu tượng khó
    /// tìm emoji đúng thì bỏ trống, đừng gán bừa — emoji sai nghĩa còn hại hơn không có.
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    /// Mẹo nhớ theo phương pháp keyword: một từ tiếng Việt NGHE GIỐNG từ tiếng Anh, rồi một
    /// câu nối hai nghĩa lại. Ví dụ với "escalate": "ét-ca-lây-tờ nghe như thang cuốn
    /// (escalator) — đẩy ticket lên thang cuốn cho tuyến trên."
    ///
    /// Đây là kỹ thuật ghi nhớ từ vựng được nghiên cứu nhiều nhất, và nó mạnh nhất khi câu nối
    /// càng cụ thể càng vô lý. Mẹo chung chung kiểu "nhớ theo ngữ cảnh" thì vô dụng.
    /// </summary>
    public string? MnemonicVi { get; set; }

    public List<string> Tags { get; set; } = [];
}

public class SentencePatternDocument
{
    public string Pattern { get; set; } = string.Empty;
    public string MeaningVi { get; set; } = string.Empty;
    public Dictionary<string, List<string>> Slots { get; set; } = [];
}

public class DialogueDocument
{
    public string ContextVi { get; set; } = string.Empty;
    public List<DialogueTurnDocument> Turns { get; set; } = [];
}

public class DialogueTurnDocument
{
    public string Speaker { get; set; } = string.Empty;
    public string En { get; set; } = string.Empty;
    public string Vi { get; set; } = string.Empty;
}

public class ListeningDocument
{
    public string ContextVi { get; set; } = string.Empty;
    public string TranscriptEn { get; set; } = string.Empty;
    public string TranscriptVi { get; set; } = string.Empty;

    /// <summary>Tốc độ đọc. Bậc thấp chậm hơn: L0 0.8 tới L4 1.3.</summary>
    public double Speed { get; set; } = 1.0;

    public string Voice { get; set; } = "en_US_female";
    public List<QuizItemDocument> Questions { get; set; } = [];
}

public class SpeakingDrillDocument
{
    /// <summary>read_aloud, shadow, hoặc respond.</summary>
    public string Kind { get; set; } = "read_aloud";

    public string ExpectedText { get; set; } = string.Empty;
    public string PromptVi { get; set; } = string.Empty;
    public string? PromptEn { get; set; }
    public string? Ipa { get; set; }

    /// <summary>Âm vị mà drill này nhắm tới. Dùng để chấm có trọng tâm.</summary>
    public List<string> FocusPhonemes { get; set; } = [];

    /// <summary>Với kind=respond: các mẫu câu trả lời chấp nhận được.</summary>
    public List<string> AcceptPatterns { get; set; } = [];
}

public class ReadingDocument
{
    /// <summary>Loại văn bản: chat, email, ticket, log, note, postmortem.</summary>
    public string Kind { get; set; } = "note";

    public string ContextVi { get; set; } = string.Empty;
    public string TextEn { get; set; } = string.Empty;
    public string TextVi { get; set; } = string.Empty;
    public List<QuizItemDocument> Questions { get; set; } = [];
}

public class WritingDocument
{
    public WritingTaskKind Kind { get; set; } = WritingTaskKind.FillBlank;

    public string PromptVi { get; set; } = string.Empty;
    public string PromptEn { get; set; } = string.Empty;
    public string? HintVi { get; set; }

    /// <summary>Với FillBlank: mỗi chỗ trống là một danh sách đáp án chấp nhận được.</summary>
    public List<List<string>> Blanks { get; set; } = [];

    /// <summary>Với Reorder: thứ tự đúng của các mảnh câu.</summary>
    public List<string> CorrectOrder { get; set; } = [];

    /// <summary>Với GuidedEmail: các ý bắt buộc phải có mặt.</summary>
    public List<string> RequiredPoints { get; set; } = [];

    public string SampleEn { get; set; } = string.Empty;
}

public class QuizItemDocument
{
    /// <summary>mcq, mcq_listen, mcq_read, true_false.</summary>
    public string Kind { get; set; } = "mcq";

    public string? PromptVi { get; set; }
    public string? PromptEn { get; set; }

    /// <summary>Với mcq_listen: văn bản để sinh audio.</summary>
    public string? AudioText { get; set; }

    public List<string> Choices { get; set; } = [];

    /// <summary>Chỉ số đáp án đúng, đếm từ 0.</summary>
    public int Answer { get; set; }

    public int Difficulty { get; set; } = 1;

    /// <summary>Với câu đọc: skim hay scan.</summary>
    public string? ReadingSkill { get; set; }

    public SkillType Skill { get; set; } = SkillType.Listening;
}

public class ExplanationDocument
{
    /// <summary>Vì sao chỗ này khó với người Việt.</summary>
    public string WhyVi { get; set; } = string.Empty;

    /// <summary>Làm thế nào để làm đúng.</summary>
    public string HowVi { get; set; } = string.Empty;

    /// <summary>Cặp đối lập để thấy rõ khác biệt. Không bắt buộc.</summary>
    public string? ContrastVi { get; set; }
}

public class CommonMistakeDocument
{
    public string Mistake { get; set; } = string.Empty;
    public string WhyVi { get; set; } = string.Empty;
    public string FixVi { get; set; } = string.Empty;

    /// <summary>Âm vị thiếu, dùng để máy tự nhận ra lỗi này khi chấm nói.</summary>
    public List<string> DetectPhonemeMissing { get; set; } = [];
}
