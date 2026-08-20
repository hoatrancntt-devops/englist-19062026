namespace EnglishForIT.Application.Learning;

public record SpeechScore(
    double PronunciationScore,
    double FluencyScore,
    double CommunicationScore,
    /// <summary>Các từ trong câu mẫu mà bộ nhận dạng không nghe thấy.</summary>
    IReadOnlyList<string> MissedWords,
    IReadOnlyList<string> FeedbackVi);

/// <summary>
/// Chấm một lượt nói, dựa trên bản ghi chữ do bộ nhận dạng trả về.
///
/// <b>Đây là chấm ở mức TỪ, không phải mức âm vị.</b> Nói rõ giới hạn này vì nó quyết định
/// cách đọc điểm: hệ thống biết học viên có nói ra đúng từ hay không, và nói với tốc độ nào,
/// nhưng KHÔNG biết họ phát âm âm /θ/ thành /t/ hay /s/. Chấm âm vị cần bộ g2p và bước
/// gióng hàng, chưa có ở bản này.
///
/// Ba trục cố ý đo ba thứ khác nhau. Một người đọc đúng từng từ nhưng ngắt quãng liên tục
/// vẫn khó nghe trong họp; một người trôi chảy mà nuốt mất từ khoá thì người nghe hiểu sai.
/// </summary>
public static class SpeechScorer
{
    /// <summary>
    /// Trọng số gộp ba trục thành một điểm.
    ///
    /// Truyền đạt nặng nhất vì mục tiêu là nói cho người ta hiểu, không phải nói cho giống
    /// người bản xứ. Đặt ở đây thay vì trong dịch vụ chấm để chỗ chấm bước Nói của bài học
    /// gộp ra đúng cùng một con số — hai công thức là hai điểm khác nhau cho cùng một lần nói.
    /// </summary>
    private const double PronunciationWeight = 0.35;
    private const double FluencyWeight = 0.20;
    private const double CommunicationWeight = 0.45;

    public static double Overall(double pronunciation, double fluency, double communication) =>
        Math.Round(
            pronunciation * PronunciationWeight
            + fluency * FluencyWeight
            + communication * CommunicationWeight, 1);

    /// <summary>Tốc độ nói tự nhiên của người bản xứ, từ mỗi phút.</summary>
    private const double NaturalWordsPerMinute = 150;

    /// <summary>Dưới mức này là chậm tới mức người nghe mất kiên nhẫn.</summary>
    private const double SlowWordsPerMinute = 70;

    public static SpeechScore Score(string expectedText, string? transcript, int durationMs)
    {
        var expected = Words(expectedText);

        if (expected.Count == 0)
        {
            return new SpeechScore(0, 0, 0, [], ["Câu mẫu rỗng nên không chấm được."]);
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return new SpeechScore(0, 0, 0, expected,
                ["Không nghe thấy gì. Kiểm tra micro rồi thử lại — nói cách micro khoảng một gang tay."]);
        }

        var actual = Words(transcript);

        // Khớp theo TẬP TỪ chứ không theo thứ tự: bộ nhận dạng hay nuốt mạo từ và giới từ,
        // mà những từ đó không phải thứ quyết định người nghe có hiểu hay không.
        var actualPool = new List<string>(actual);
        var matched = new List<string>();
        var missed = new List<string>();

        foreach (var word in expected)
        {
            // Chấp nhận lệch một ký tự: bộ nhận dạng hay trả "folders" thay vì "folder",
            // mà khác biệt đó không phải thứ đang cần dạy.
            var index = actualPool.FindIndex(a =>
                a == word || TextMatching.EditDistance(a, word) <= (word.Length <= 4 ? 0 : 1));

            if (index >= 0)
            {
                matched.Add(word);
                actualPool.RemoveAt(index);
            }
            else
            {
                missed.Add(word);
            }
        }

        var pronunciation = Math.Round(matched.Count * 100.0 / expected.Count, 1);

        var fluency = ScoreFluency(actual.Count, durationMs);

        // Truyền đạt tính trên từ nội dung: thiếu "the" không ai hiểu sai, thiếu "not" thì có.
        var contentWords = expected.Where(IsContentWord).ToList();

        var communication = contentWords.Count == 0
            ? pronunciation
            : Math.Round(
                contentWords.Count(w => !missed.Contains(w)) * 100.0 / contentWords.Count, 1);

        return new SpeechScore(
            pronunciation,
            fluency,
            communication,
            missed,
            BuildFeedback(pronunciation, fluency, communication, missed, actual.Count, durationMs));
    }

    /// <summary>
    /// Điểm trôi chảy theo tốc độ nói.
    ///
    /// Chậm quá thì người nghe mất kiên nhẫn; nhanh quá thường là đọc vẹt chứ không phải nói.
    /// Khoảng tự nhiên rộng vì học viên mới cần được nói chậm mà không bị phạt nặng.
    /// </summary>
    private static double ScoreFluency(int wordCount, int durationMs)
    {
        if (wordCount == 0 || durationMs <= 0)
        {
            return 0;
        }

        var wpm = wordCount / (durationMs / 60_000.0);

        if (wpm >= SlowWordsPerMinute && wpm <= NaturalWordsPerMinute * 1.4)
        {
            return 100;
        }

        if (wpm < SlowWordsPerMinute)
        {
            // Tuyến tính từ 0 tới ngưỡng chậm: nói nửa tốc độ tối thiểu được 50 điểm.
            return Math.Round(Math.Max(0, wpm / SlowWordsPerMinute * 100), 1);
        }

        // Quá nhanh: trừ dần, sàn 60 vì nói nhanh vẫn hiểu được, chỉ khó nghe.
        var excess = wpm - NaturalWordsPerMinute * 1.4;
        return Math.Round(Math.Max(60, 100 - excess / 2), 1);
    }

    private static List<string> BuildFeedback(
        double pronunciation, double fluency, double communication,
        IReadOnlyList<string> missed, int wordCount, int durationMs)
    {
        var notes = new List<string>();

        if (pronunciation >= 90 && fluency >= 80 && communication >= 90)
        {
            notes.Add("Nói rõ và đủ ý. Đây là mức dùng được trong họp.");
            return notes;
        }

        if (missed.Count > 0)
        {
            var shown = string.Join(", ", missed.Take(5));
            notes.Add(missed.Count <= 5
                ? $"Máy không nghe được: {shown}. Đọc chậm lại đúng những từ này."
                : $"Máy không nghe được {missed.Count} từ, trong đó có: {shown}.");
        }

        if (fluency < 60 && wordCount > 0 && durationMs > 0)
        {
            var wpm = (int)Math.Round(wordCount / (durationMs / 60_000.0));
            notes.Add($"Tốc độ khoảng {wpm} từ mỗi phút. Người bản xứ nói quanh 150 — " +
                      "ngắt câu theo cụm thay vì theo từng từ sẽ tự nhiên hơn.");
        }

        if (communication < 70)
        {
            notes.Add("Thiếu mất từ mang nghĩa chính nên người nghe dễ hiểu sai. " +
                      "Nhấn vào danh từ và động từ, đừng nhấn vào mạo từ.");
        }

        if (notes.Count == 0)
        {
            notes.Add("Gần đạt. Đọc lại một lần nữa, chú ý đọc trọn âm cuối của mỗi từ.");
        }

        return notes;
    }

    /// <summary>
    /// Từ mang nghĩa chính. Danh sách từ chức năng cố ý ngắn — chỉ gồm những từ mà thiếu đi
    /// người nghe vẫn hiểu đúng.
    /// </summary>
    private static bool IsContentWord(string word) =>
        !FunctionWords.Contains(word);

    private static readonly HashSet<string> FunctionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "are", "am", "was", "were", "be", "been",
        "to", "of", "in", "on", "at", "for", "and", "or", "it", "this", "that",
    };

    private static List<string> Words(string text) =>
        [.. TextMatching.Normalize(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
