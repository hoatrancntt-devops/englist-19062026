namespace EnglishForIT.Application.Content;

/// <summary>
/// Cổng kiểm định cho bộ từ vựng tần suất cao.
///
/// Siết chặt hơn cổng của bài học ở một chỗ: MỌI từ đều bắt buộc có emoji và mẹo nhớ. Từ vựng
/// trong bài học có 609 mục ra đời trước hai trường đó nên phải nới, còn đây là nội dung mới —
/// không có lý do gì để cho nợ.
/// </summary>
public static class VocabDeckValidator
{
    /// <summary>Mỗi bộ đúng một bậc tần suất 100 từ. Nới ±20 để người soạn không phải nhồi cho tròn.</summary>
    private const int MinWords = 80;
    private const int MaxWords = 120;

    /// <summary>Dưới ngưỡng này thì mẹo nhớ chắc chắn thiếu từ nghe giống hoặc thiếu câu nối.</summary>
    private const int MinMnemonicLength = 20;

    private const int MaxEmojiLength = 16;

    public static IReadOnlyList<ValidationIssue> Validate(VocabDeckDocument doc)
    {
        var issues = new List<ValidationIssue>();
        var code = string.IsNullOrWhiteSpace(doc.Code) ? "(thiếu code)" : doc.Code;

        void Add(string rule, string message) => issues.Add(new ValidationIssue(rule, code, message));

        if (string.IsNullOrWhiteSpace(doc.Code))
        {
            Add("V001", "Thiếu code.");
        }

        if (string.IsNullOrWhiteSpace(doc.TitleVi))
        {
            Add("V002", "Thiếu title_vi.");
        }

        if (string.IsNullOrWhiteSpace(doc.ContextVi))
        {
            Add("V003", "Thiếu context_vi — học viên cần biết vì sao nhóm từ này đáng học trước.");
        }

        if (doc.Band < 1)
        {
            Add("V004", $"band = {doc.Band}, phải từ 1 trở lên.");
        }

        if (doc.Words.Count is < MinWords or > MaxWords)
        {
            Add("V010", $"Bộ có {doc.Words.Count} từ, ngoài khoảng {MinWords}-{MaxWords}.");
        }

        foreach (var word in doc.Words)
        {
            var term = string.IsNullOrWhiteSpace(word.Term) ? "(thiếu term)" : word.Term;

            if (string.IsNullOrWhiteSpace(word.Term))
            {
                Add("V020", "Có mục thiếu term.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(word.Ipa))
            {
                Add("V021", $"Từ \"{term}\" thiếu ipa. Học viên phải biết đọc thế nào trước khi nói lại.");
            }

            if (string.IsNullOrWhiteSpace(word.MeaningVi))
            {
                Add("V022", $"Từ \"{term}\" thiếu meaning_vi.");
            }

            if (string.IsNullOrWhiteSpace(word.Chunk))
            {
                Add("V023", $"Từ \"{term}\" thiếu chunk. Từ rời không dùng được, phải có cụm dùng ngay.");
            }

            if (string.IsNullOrWhiteSpace(word.Emoji))
            {
                Add("V024", $"Từ \"{term}\" thiếu emoji.");
            }
            else if (word.Emoji.Length > MaxEmojiLength)
            {
                Add("V025", $"Từ \"{term}\": ô emoji dài {word.Emoji.Length} ký tự, có vẻ là câu chữ.");
            }

            if (string.IsNullOrWhiteSpace(word.MnemonicVi))
            {
                Add("V026", $"Từ \"{term}\" thiếu mnemonic_vi.");
            }
            else if (word.MnemonicVi.Trim().Length < MinMnemonicLength)
            {
                Add("V027",
                    $"Từ \"{term}\": mẹo nhớ chỉ {word.MnemonicVi.Trim().Length} ký tự, quá ngắn để có nghĩa.");
            }
        }

        // Trùng trong cùng một bộ.
        var duplicates = doc.Words
            .Where(w => !string.IsNullOrWhiteSpace(w.Term))
            .GroupBy(w => Normalize(w.Term))
            .Where(g => g.Count() > 1)
            .Select(g => g.First().Term);

        foreach (var term in duplicates)
        {
            Add("V030", $"Từ \"{term}\" xuất hiện nhiều lần trong cùng một bộ.");
        }

        return issues;
    }

    /// <summary>
    /// Kiểm chéo giữa các bộ và với từ vựng đã dạy trong bài học.
    ///
    /// Đây là luật quan trọng nhất của cả cổng: nếu bộ từ vựng lặp lại những từ mà 101 bài học
    /// đã dạy thì con số "1.000 từ mới" là sai, và học viên tốn thời gian học lại thứ đã biết.
    /// </summary>
    public static IReadOnlyList<ValidationIssue> ValidateAcross(
        IReadOnlyList<VocabDeckDocument> decks,
        IReadOnlyCollection<string> termsTaughtInLessons)
    {
        var issues = new List<ValidationIssue>();
        var lessonTerms = termsTaughtInLessons.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var deck in decks)
        {
            foreach (var word in deck.Words.Where(w => !string.IsNullOrWhiteSpace(w.Term)))
            {
                var key = Normalize(word.Term);

                if (lessonTerms.Contains(key))
                {
                    issues.Add(new ValidationIssue(
                        "V040", deck.Code,
                        $"Từ \"{word.Term}\" đã được dạy trong bài học. Bộ từ vựng chỉ nhận từ MỚI."));
                }

                if (seen.TryGetValue(key, out var firstDeck))
                {
                    issues.Add(new ValidationIssue(
                        "V041", deck.Code,
                        $"Từ \"{word.Term}\" đã có trong bộ {firstDeck}."));
                }
                else
                {
                    seen[key] = deck.Code;
                }
            }
        }

        // Mã bộ trùng nhau thì seeder ghi đè lẫn nhau trong im lặng.
        var duplicateCodes = decks
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var code in duplicateCodes)
        {
            issues.Add(new ValidationIssue("V042", code, $"Mã bộ \"{code}\" bị dùng cho nhiều file."));
        }

        return issues;
    }

    /// <summary>So khớp không phân biệt hoa thường và khoảng trắng thừa.</summary>
    private static string Normalize(string term) =>
        string.Join(' ', term.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
