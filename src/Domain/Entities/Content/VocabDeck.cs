using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Entities.Identity;

namespace EnglishForIT.Domain.Entities.Content;

/// <summary>
/// Một bộ từ vựng tần suất cao.
///
/// Tách khỏi bảng bài học vì đây không phải bài học: nó không dạy tình huống và không mở khoá
/// gì trên lộ trình, nó chỉ dựng vốn từ. Gộp chung vào lessons thì mọi truy vấn lộ trình phải
/// nhớ loại trừ nó, và sớm muộn có chỗ quên.
/// </summary>
public class VocabDeck : Entity
{
    public required string Code { get; set; }
    public required string TitleVi { get; set; }
    public required string ContextVi { get; set; }

    /// <summary>Bậc tần suất, 1 là nhóm thông dụng nhất. Quyết định thứ tự học.</summary>
    public int Band { get; set; }

    /// <summary>Hash của file nguồn. Không đổi thì seeder bỏ qua.</summary>
    public required string SourceHash { get; set; }

    public List<VocabWord> Words { get; set; } = [];
}

/// <summary>Một từ trong bộ. Cùng khuôn trường với từ vựng của bài học nên giao diện dùng chung.</summary>
public class VocabWord : Entity
{
    public Guid DeckId { get; set; }
    public VocabDeck? Deck { get; set; }

    public required string Term { get; set; }
    public required string Ipa { get; set; }
    public required string MeaningVi { get; set; }

    /// <summary>Cụm dùng được ngay. Học từ rời thì không biết đặt vào câu thế nào.</summary>
    public required string Chunk { get; set; }

    public string? Emoji { get; set; }
    public string? MnemonicVi { get; set; }

    public int OrderIndex { get; set; }
}

/// <summary>
/// Tiến độ của một học viên với một từ.
///
/// Có phần giãn cách ôn tập riêng chứ không dùng review_queue: bảng đó khoá cứng vào lesson_items
/// bằng khoá ngoại, và làm nó nhận nhiều loại item sẽ phá mất ràng buộc toàn vẹn đang bảo vệ nó.
/// </summary>
public class VocabWordProgress : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid WordId { get; set; }
    public VocabWord? Word { get; set; }

    /// <summary>Điểm nói lại cao nhất từng đạt. Học lại không bao giờ làm tụt con số này.</summary>
    public double BestScore { get; set; }

    public int AttemptCount { get; set; }

    /// <summary>Lần đầu đạt ngưỡng. Ghi một lần, không dời.</summary>
    public DateTimeOffset? FirstLearnedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Hẹn ôn lại. Ôn ngắt quãng là thứ biến vốn từ ngắn hạn thành vốn từ dùng được.</summary>
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.UtcNow;

    public int IntervalDays { get; set; } = 1;

    /// <summary>Hệ số giãn, cùng quy ước với hàng ôn tập của bài học.</summary>
    public double Ease { get; set; } = 2.5;
}
