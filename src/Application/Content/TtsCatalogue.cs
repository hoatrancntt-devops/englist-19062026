using System.Security.Cryptography;
using System.Text;

namespace EnglishForIT.Application.Content;

/// <summary>Một đoạn tiếng Anh cần đọc thành tiếng, kèm khoá file của nó.</summary>
public record TtsEntry(string Hash, string Text);

/// <summary>
/// Danh mục các đoạn tiếng Anh có nút bấm nghe, và cách đặt tên file audio cho từng đoạn.
///
/// Tên file là hash của chính đoạn văn bản. Nhờ vậy hai phía không cần thoả thuận gì thêm:
/// bước sinh audio ghi ra <c>&lt;hash&gt;.wav</c>, còn khi học viên bấm nghe thì API băm lại
/// đoạn văn bản nhận được và tìm đúng file đó. Sửa một câu trong YAML là hash đổi, file cũ
/// thành mồ côi và file mới được sinh — không cần đánh phiên bản thủ công.
///
/// <b>Hàm băm phải dùng chung ở cả hai phía.</b> Lúc sinh mà chuẩn hoá kiểu này, lúc phục vụ
/// lại chuẩn hoá kiểu khác thì mọi lượt tra đều trượt, và triệu chứng là "có file mà vẫn im
/// lặng" — rất tốn thời gian để lần ra.
///
/// Tốc độ đọc KHÔNG nằm trong hash. Một câu chỉ sinh một file ở tốc độ chuẩn, còn nhanh chậm
/// do trình duyệt chỉnh bằng playbackRate. Đưa tốc độ vào hash thì cùng một câu phải sinh ba
/// bốn bản chỉ khác nhau nhịp đọc.
/// </summary>
public static class TtsCatalogue
{
    /// <summary>Piper ghi ra WAV. Không có ffmpeg trong ảnh nên không nén thêm.</summary>
    public const string FileExtension = ".wav";

    /// <summary>
    /// Giới hạn độ dài để một đoạn hỏng trong nội dung không sinh ra file audio khổng lồ,
    /// và để URL tra cứu không vượt giới hạn của proxy.
    /// </summary>
    public const int MaxTextLength = 2000;

    /// <summary>
    /// Gộp mọi khoảng trắng thành một dấu cách và cắt hai đầu.
    ///
    /// Cần bước này vì transcript trong YAML viết bằng block scalar nên mang theo xuống dòng
    /// và thụt lề, còn chuỗi trình duyệt gửi lên đã qua JSON. Hai bên nhìn thì giống nhau
    /// nhưng byte thì không.
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                lastWasSpace = true;
                continue;
            }

            if (lastWasSpace && builder.Length > 0)
            {
                builder.Append(' ');
            }

            lastWasSpace = false;
            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Khoá file của một đoạn. Chuỗi rỗng trả về rỗng để nơi gọi biết là không có gì để đọc.
    ///
    /// Lấy 32 ký tự hex đầu (128 bit) chứ không lấy cả 64: đủ để không đụng nhau trong một
    /// giáo trình vài nghìn câu, mà tên file ngắn hơn hẳn khi cần đọc log bằng mắt.
    /// </summary>
    public static string HashOf(string text)
    {
        var normalized = Normalize(text);

        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));

        return Convert.ToHexStringLower(digest)[..32];
    }

    /// <summary>
    /// Gom mọi đoạn tiếng Anh mà giao diện có nút bấm nghe, đã khử trùng lặp theo hash.
    ///
    /// Chỉ gom đúng những chỗ hiện có nút nghe. Sinh cho cả những đoạn chưa ai nghe được là
    /// đốt thời gian máy và phình thư mục audio mà không ai được lợi.
    /// </summary>
    public static IReadOnlyList<TtsEntry> Collect(
        IEnumerable<LessonDocument> lessons,
        IEnumerable<PlacementDocument> placements,
        IEnumerable<RoleplayDocument> roleplays)
    {
        var seen = new Dictionary<string, TtsEntry>(StringComparer.Ordinal);

        void Add(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var normalized = Normalize(text);

            if (normalized.Length == 0 || normalized.Length > MaxTextLength)
            {
                return;
            }

            var hash = HashOf(normalized);
            seen.TryAdd(hash, new TtsEntry(hash, normalized));
        }

        foreach (var lesson in lessons)
        {
            // Đoạn nghe của bài — dài nhất và cũng là chỗ giọng đồng nhất đáng giá nhất.
            Add(lesson.Listening?.TranscriptEn);

            // Câu mẫu của drill nói: học viên bấm nghe trước khi thu giọng mình.
            foreach (var drill in lesson.SpeakingDrills)
            {
                Add(drill.ExpectedText);
            }

            // Câu hỏi nghe trong phần kiểm tra.
            foreach (var item in lesson.Quiz)
            {
                Add(item.AudioText);
            }

            // Từng lượt hội thoại. Ngắn, rẻ, và là chỗ học viên hay muốn nghe lại từng câu.
            foreach (var turn in lesson.Dialogue?.Turns ?? [])
            {
                Add(turn.En);
            }
        }

        foreach (var item in placements.SelectMany(p => p.Items))
        {
            Add(item.Prompt.AudioText);
        }

        foreach (var node in roleplays.SelectMany(r => r.Nodes))
        {
            Add(node.PartnerLineEn);
        }

        return [.. seen.Values.OrderBy(e => e.Hash, StringComparer.Ordinal)];
    }
}
