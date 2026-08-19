using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Progress;

/// <summary>Một lượt thi xếp lớp.</summary>
public class PlacementAttempt : Entity, IConcurrencyStamped
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid FormId { get; set; }
    public PlacementForm? Form { get; set; }

    public PlacementAttemptStatus Status { get; set; } = PlacementAttemptStatus.InProgress;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Hạn nộp tính từ lúc bắt đầu. Quá hạn thì chỉ chấm phần đã trả lời.</summary>
    public DateTimeOffset DeadlineAt { get; set; }

    /// <summary>Bậc kết luận. Null khi chưa nộp.</summary>
    public CefrLevel? ResultLevel { get; set; }

    /// <summary>Điểm bốn trục kỹ năng, JSONB. Đây là dữ liệu vẽ biểu đồ bốn trục.</summary>
    public Dictionary<SkillType, double> SkillScores { get; set; } = [];

    /// <summary>Trục phụ từ vựng và ngữ pháp, thang 0-100.</summary>
    public double VocabGrammarScore { get; set; }

    /// <summary>Tỷ lệ câu trả lời quá nhanh so với ngưỡng. Cao thì nghi đoán mò.</summary>
    public double FastAnswerRatio { get; set; }

    /// <summary>Tự đánh giá từ câu Likert, thang 0-100. Đối chiếu với điểm thật để phát hiện lệch.</summary>
    public double SelfRatedScore { get; set; }

    /// <summary>Giải thích tiếng Việt sinh lúc chấm, JSONB.</summary>
    public string? ExplanationJson { get; set; }

    public uint RowVersion { get; set; }

    public ICollection<PlacementAnswer> Answers { get; set; } = [];
}

/// <summary>
/// Câu trả lời. Điểm chấm tại server ngay khi nhận, nhưng không trả về client
/// cho tới lúc nộp toàn bài.
/// </summary>
public class PlacementAnswer : Entity
{
    public Guid AttemptId { get; set; }
    public PlacementAttempt? Attempt { get; set; }

    public Guid ItemId { get; set; }
    public PlacementFormItem? Item { get; set; }

    /// <summary>Câu trả lời thô của học viên, JSONB.</summary>
    public required string ResponseJson { get; set; }

    /// <summary>Điểm 0-100. Giữ phía server tới lúc nộp.</summary>
    public double Score { get; set; }

    public bool IsCorrect { get; set; }

    /// <summary>Số giây từ lúc hiện câu tới lúc trả lời. Đầu vào của chỉ số đoán mò.</summary>
    public int ResponseSeconds { get; set; }
}

/// <summary>
/// Điểm nói của một câu speaking trong đề. Tách bảng vì chấm phát âm chạy bất đồng bộ:
/// học viên ghi âm xong đi tiếp, speech service chấm sau.
/// </summary>
public class PlacementSpeakingScore : Entity
{
    public Guid AttemptId { get; set; }
    public Guid ItemId { get; set; }

    public double PronunciationScore { get; set; }
    public double FluencyScore { get; set; }
    public double CommunicationScore { get; set; }

    /// <summary>Văn bản ASR nhận được. Chỉ để đối chiếu, không hiển thị nguyên văn cho học viên.</summary>
    public string? TranscriptEn { get; set; }

    /// <summary>Âm vị thiếu hoặc sai, JSONB mảng.</summary>
    public string? PhonemeIssuesJson { get; set; }

    public bool Scored { get; set; }
    public DateTimeOffset? ScoredAt { get; set; }
}

/// <summary>
/// Ghi âm của học viên. File lưu trên volume nội bộ, không bao giờ gửi ra dịch vụ bên thứ ba.
/// </summary>
public class SpeechAttempt : Entity
{
    public Guid UserId { get; set; }

    /// <summary>Bối cảnh: lesson_activity, placement_item, roleplay_node.</summary>
    public required string ContextType { get; set; }

    public Guid? ContextId { get; set; }

    public required string ExpectedText { get; set; }
    public string? TranscriptEn { get; set; }

    public double PronunciationScore { get; set; }
    public double FluencyScore { get; set; }
    public double CommunicationScore { get; set; }

    /// <summary>Nhận xét tiếng Việt sinh từ âm vị sai.</summary>
    public string? FeedbackViJson { get; set; }

    /// <summary>Đường dẫn tương đối tới file ghi âm. Job dọn dẹp xoá sau N ngày.</summary>
    public string? AudioRelativePath { get; set; }

    public int DurationMs { get; set; }
}
