using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Content;

public record ValidationIssue(string Code, string LessonCode, string Message)
{
    public override string ToString() => $"[{Code}] {LessonCode}: {Message}";
}

/// <summary>
/// Cổng chất lượng nội dung.
///
/// Nguyên tắc: mọi quy tắc ở đây đều là thứ đã từng làm hỏng trải nghiệm học viên
/// trong bản trước, không phải sở thích cá nhân. Mỗi quy tắc có một câu giải thích
/// vì sao nó tồn tại — quy tắc không giải thích được thì nên xoá.
/// </summary>
public class LessonValidator
{
    /// <summary>Tải từ vựng của người lớn: 5–8 mục mỗi buổi (Nation và Webb, 2008).</summary>
    private const int MinVocabulary = 5;
    private const int MaxVocabulary = 8;

    /// <summary>Bài dài hơn 12 phút thì học viên bỏ giữa chừng.</summary>
    private const int MaxEstimatedMinutes = 12;

    private const int MinCommonMistakes = 2;
    private const int MinSpeakingDrills = 4;

    /// <summary>Kiểm một bài đứng riêng. Các quy tắc cần biết cả tập nằm ở <see cref="ValidateSet"/>.</summary>
    public IReadOnlyList<ValidationIssue> ValidateOne(LessonDocument doc)
    {
        var issues = new List<ValidationIssue>();
        var code = string.IsNullOrWhiteSpace(doc.Code) ? "(thieu-ma)" : doc.Code;

        void Add(string ruleCode, string message) => issues.Add(new ValidationIssue(ruleCode, code, message));

        // --- Trường bắt buộc ---
        if (string.IsNullOrWhiteSpace(doc.Code))
        {
            Add("E001", "Thiếu code.");
        }

        if (string.IsNullOrWhiteSpace(doc.Slug))
        {
            Add("E002", "Thiếu slug.");
        }

        if (string.IsNullOrWhiteSpace(doc.TitleVi))
        {
            Add("E003", "Thiếu title_vi.");
        }

        // Không có giải thích tiếng Việt thì học viên mất gốc không hiểu vì sao mình sai,
        // và phần AI cũng không có gì để fallback khi không cấu hình khoá API.
        if (string.IsNullOrWhiteSpace(doc.Explanation.WhyVi) || string.IsNullOrWhiteSpace(doc.Explanation.HowVi))
        {
            Add("E010", "Thiếu explanation.why_vi hoặc explanation.how_vi. Đây là phần thay thế AI khi không có khoá API.");
        }

        if (doc.CommonMistakes.Count < MinCommonMistakes)
        {
            Add("E011", $"Chỉ có {doc.CommonMistakes.Count} common_mistakes, cần tối thiểu {MinCommonMistakes}. " +
                        "Đây là dữ liệu để máy nhận ra lỗi khi chấm nói.");
        }

        foreach (var mistake in doc.CommonMistakes.Where(m =>
                     string.IsNullOrWhiteSpace(m.WhyVi) || string.IsNullOrWhiteSpace(m.FixVi)))
        {
            Add("E012", $"common_mistake \"{Trim(mistake.Mistake)}\" thiếu why_vi hoặc fix_vi. " +
                        "Chỉ ra lỗi mà không nói cách sửa là bỏ mặc học viên.");
        }

        // --- Thời lượng ---
        if (doc.EstimatedMinutes > MaxEstimatedMinutes)
        {
            Add("E020", $"estimated_minutes = {doc.EstimatedMinutes}, vượt trần {MaxEstimatedMinutes}.");
        }

        if (doc.EstimatedMinutes < 3)
        {
            Add("E021", $"estimated_minutes = {doc.EstimatedMinutes}, quá ngắn để dạy được gì.");
        }

        // --- Từ vựng ---
        if (doc.Vocabulary.Count < MinVocabulary || doc.Vocabulary.Count > MaxVocabulary)
        {
            Add("E030", $"Có {doc.Vocabulary.Count} mục từ vựng, ngoài khoảng {MinVocabulary}-{MaxVocabulary}. " +
                        "Nhồi nhiều hơn thì học viên nhớ được ít hơn.");
        }

        foreach (var vocab in doc.Vocabulary.Where(v => string.IsNullOrWhiteSpace(v.Chunk)))
        {
            Add("E031", $"Từ \"{vocab.Term}\" thiếu chunk. Học từ rời không dùng được, phải có cụm dùng ngay.");
        }

        foreach (var vocab in doc.Vocabulary.Where(v => string.IsNullOrWhiteSpace(v.MeaningVi)))
        {
            Add("E032", $"Từ \"{vocab.Term}\" thiếu meaning_vi.");
        }

        // --- Trọng số mastery ---
        if (doc.MasteryWeights.Count == 0)
        {
            Add("E040", "Thiếu mastery_weights.");
        }
        else
        {
            var total = doc.MasteryWeights.Values.Sum();
            if (Math.Abs(total - 1.0) > 0.001)
            {
                Add("E041", $"Tổng mastery_weights = {total:0.###}, phải bằng 1.0.");
            }

            foreach (var (skill, weight) in doc.MasteryWeights.Where(w => w.Value < 0))
            {
                Add("E042", $"mastery_weights[{skill}] = {weight}, không được âm.");
            }
        }

        // --- Kỹ năng khai báo phải khớp phần thực có ---
        var actual = InferSupportedSkills(doc);

        if (actual.Count == 0)
        {
            Add("E050", "Bài không có phần nào dạy được kỹ năng nào.");
        }

        foreach (var declared in doc.SupportedSkills.Where(s => !actual.Contains(s)))
        {
            Add("E051", $"Khai supported_skills có {declared} nhưng bài không có phần tương ứng. " +
                        "Đây là nguyên nhân số một của lỗi \"chọn chế độ một kỹ năng rồi mở bài thấy trống\".");
        }

        // Trọng số cho kỹ năng mà bài không dạy sẽ làm mastery không bao giờ đạt 100.
        foreach (var weighted in doc.MasteryWeights.Where(w => w.Value > 0 && !actual.Contains(w.Key)))
        {
            Add("E052", $"mastery_weights có {weighted.Key} = {weighted.Value} nhưng bài không dạy kỹ năng đó. " +
                        "Học viên sẽ không bao giờ đạt đủ mastery.");
        }

        // --- Phần nói ---
        var isSpeakingHeavy = doc.MasteryWeights.GetValueOrDefault(SkillType.Speaking) >= 0.3;
        if (isSpeakingHeavy && doc.SpeakingDrills.Count < MinSpeakingDrills)
        {
            Add("E060", $"Bài trọng tâm nói chỉ có {doc.SpeakingDrills.Count} drill, cần tối thiểu {MinSpeakingDrills}.");
        }

        foreach (var drill in doc.SpeakingDrills.Where(d =>
                     d.Kind != "respond" && string.IsNullOrWhiteSpace(d.ExpectedText)))
        {
            Add("E061", $"Drill \"{Trim(drill.PromptVi)}\" thiếu expected_text — không có gì để chấm.");
        }

        foreach (var drill in doc.SpeakingDrills.Where(d =>
                     d.Kind == "respond" && d.AcceptPatterns.Count == 0))
        {
            Add("E062", $"Drill respond \"{Trim(drill.PromptVi)}\" thiếu accept_patterns.");
        }

        // --- Câu hỏi ---
        foreach (var item in AllQuizItems(doc))
        {
            if (item.Choices.Count < 2)
            {
                Add("E070", $"Câu \"{Trim(item.PromptVi ?? item.PromptEn ?? "")}\" có dưới 2 lựa chọn.");
            }
            else if (item.Answer < 0 || item.Answer >= item.Choices.Count)
            {
                Add("E071", $"Câu \"{Trim(item.PromptVi ?? item.PromptEn ?? "")}\" có answer = {item.Answer}, " +
                            $"ngoài phạm vi 0..{item.Choices.Count - 1}.");
            }

            if (item.Choices.Count != item.Choices.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                Add("E072", $"Câu \"{Trim(item.PromptVi ?? item.PromptEn ?? "")}\" có lựa chọn trùng nhau.");
            }
        }

        // --- Phần viết ---
        if (doc.Writing is { } writing)
        {
            if (writing.Kind == WritingTaskKind.FillBlank && writing.Blanks.Count == 0)
            {
                Add("E080", "Bài viết dạng fill_blank nhưng không có blanks.");
            }

            if (writing.Kind == WritingTaskKind.Reorder && writing.CorrectOrder.Count < 3)
            {
                Add("E081", "Bài viết dạng reorder cần tối thiểu 3 mảnh.");
            }

            if (writing.Kind == WritingTaskKind.GuidedEmail && writing.RequiredPoints.Count == 0)
            {
                Add("E082", "Bài viết dạng guided_email cần required_points để chấm được.");
            }

            if (string.IsNullOrWhiteSpace(writing.SampleEn))
            {
                Add("E083", "Bài viết thiếu sample_en. Học viên sai mà không có mẫu để đối chiếu thì không học được gì.");
            }
        }

        // --- Hình minh hoạ ---
        // Gõ sai khoá thì bài mất hình mà không có lỗi nào — nên chặn ở đây.
        if (!string.IsNullOrWhiteSpace(doc.Illustration) && !IllustrationCatalogue.IsKnown(doc.Illustration))
        {
            Add("E110", $"Khoá hình minh hoạ \"{doc.Illustration}\" không có trong danh mục. " +
                        $"Các khoá hợp lệ: {string.Join(", ", IllustrationCatalogue.All.Order())}.");
        }

        // --- Dấu gạch chéo lọt vào phần giải thích ---
        // Trong khối block scalar của YAML (dấu > hoặc |), \" KHÔNG phải ký tự thoát.
        // Nó đi thẳng vào chuỗi và học viên nhìn thấy nguyên dấu gạch chéo giữa câu.
        // Lỗi này không làm hỏng gì nên không ai phát hiện — chỉ người học nhìn thấy.
        foreach (var (field, text) in ProseFields(doc))
        {
            if (text.Contains("\\\"", StringComparison.Ordinal))
            {
                Add("E111", $"{field} chứa \\\" — trong khối block scalar thì đó là hai ký tự thật, " +
                            "không phải dấu nháy. Dùng dấu nháy cong “ ” hoặc chuyển sang chuỗi trong ngoặc kép.");
            }
        }

        // --- Tiên quyết ---
        foreach (var prereq in doc.Prerequisites.Where(p => p.Lesson == doc.Code))
        {
            Add("E090", $"Bài tự tiên quyết chính nó ({prereq.Lesson}).");
        }

        foreach (var prereq in doc.Prerequisites.Where(p => p.MinMastery is < 0 or > 100))
        {
            Add("E091", $"min_mastery = {prereq.MinMastery} cho {prereq.Lesson}, ngoài khoảng 0-100.");
        }

        // --- Vị trí đáp án ---
        //
        // Đáp án dồn về một ô là lỗ hổng chết người: học viên chọn mãi ô đó là qua bài mà
        // không cần đọc câu hỏi, và qua được cả bài thi vượt. Không có con số nào trong
        // điểm số cho thấy chuyện đó đã xảy ra.
        var answers = AllQuizItems(doc)
            .Where(i => i.Choices.Count >= 2 && i.Answer >= 0 && i.Answer < i.Choices.Count)
            .Select(i => i.Answer)
            .ToList();

        if (answers.Count >= 4)
        {
            var mostCommon = answers.GroupBy(a => a).OrderByDescending(g => g.Count()).First();

            if (mostCommon.Count() * 2 > answers.Count)
            {
                Add("E072", $"{mostCommon.Count()} trên {answers.Count} câu có đáp án ở vị trí {mostCommon.Key}. " +
                            "Chọn mãi một ô là qua bài. Trải đáp án ra các vị trí khác nhau.");
            }
        }

        return issues;
    }

    /// <summary>Các quy tắc chỉ kiểm được khi có cả tập bài: mã trùng, cạnh treo, chu trình, thứ tự bậc.</summary>
    public IReadOnlyList<ValidationIssue> ValidateSet(IReadOnlyCollection<LessonDocument> docs)
    {
        var issues = new List<ValidationIssue>();

        foreach (var doc in docs)
        {
            issues.AddRange(ValidateOne(doc));
        }

        // --- Mã và slug trùng ---
        foreach (var group in docs.GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            issues.Add(new ValidationIssue("E100", group.Key, $"Mã bài xuất hiện {group.Count()} lần."));
        }

        foreach (var group in docs.GroupBy(d => d.Slug, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key)))
        {
            issues.Add(new ValidationIssue("E101", group.First().Code, $"Slug \"{group.Key}\" bị trùng."));
        }

        var byCode = docs
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // --- Cạnh treo ---
        foreach (var doc in docs)
        {
            foreach (var prereq in doc.Prerequisites.Where(p => !byCode.ContainsKey(p.Lesson)))
            {
                issues.Add(new ValidationIssue("E102", doc.Code,
                    $"Tiên quyết trỏ tới bài không tồn tại: {prereq.Lesson}."));
            }
        }

        // --- Bất biến thứ tự bậc ---
        // Không cạnh nào được đi từ bậc cao xuống bậc thấp: bài dễ bị khoá sau bài khó
        // là lỗi chặn publish, không phải chuyện thẩm mỹ.
        foreach (var doc in docs)
        {
            foreach (var prereq in doc.Prerequisites)
            {
                if (byCode.TryGetValue(prereq.Lesson, out var required) && required.Level > doc.Level)
                {
                    issues.Add(new ValidationIssue("E103", doc.Code,
                        $"Bài bậc {doc.Level} tiên quyết bài bậc cao hơn {required.Level} ({prereq.Lesson})."));
                }
            }
        }

        // --- Chu trình ---
        // Dò trên byCode chứ không trên docs: docs có thể chứa mã trùng (đã báo ở E100),
        // và dựng bảng màu thẳng từ docs sẽ ném lỗi khoá trùng trước khi kịp trả về issue nào.
        foreach (var cycle in FindCycles(byCode))
        {
            issues.Add(new ValidationIssue("E104", cycle[0], $"DAG có chu trình: {string.Join(" -> ", cycle)}."));
        }

        return issues;
    }

    /// <summary>Suy ra kỹ năng bài dạy được từ các phần thực có mặt.</summary>
    public static HashSet<SkillType> InferSupportedSkills(LessonDocument doc)
    {
        var skills = new HashSet<SkillType>();

        if (doc.Listening is not null || doc.Dialogue is not null)
        {
            skills.Add(SkillType.Listening);
        }

        if (doc.SpeakingDrills.Count > 0)
        {
            skills.Add(SkillType.Speaking);
        }

        if (doc.Reading is not null)
        {
            skills.Add(SkillType.Reading);
        }

        if (doc.Writing is not null)
        {
            skills.Add(SkillType.Writing);
        }

        return skills;
    }

    private static IEnumerable<QuizItemDocument> AllQuizItems(LessonDocument doc)
    {
        foreach (var item in doc.Quiz)
        {
            yield return item;
        }

        foreach (var item in doc.Listening?.Questions ?? [])
        {
            yield return item;
        }

        foreach (var item in doc.Reading?.Questions ?? [])
        {
            yield return item;
        }
    }

    /// <summary>Tìm chu trình bằng DFS ba màu. Trả về đường đi để người soạn biết sửa ở đâu.</summary>
    private static List<List<string>> FindCycles(Dictionary<string, LessonDocument> byCode)
    {
        const int White = 0, Grey = 1, Black = 2;

        var colour = byCode.Keys.ToDictionary(code => code, _ => White, StringComparer.OrdinalIgnoreCase);
        var cycles = new List<List<string>>();
        var stack = new List<string>();

        void Visit(string code)
        {
            colour[code] = Grey;
            stack.Add(code);

            foreach (var prereq in byCode[code].Prerequisites)
            {
                if (!colour.TryGetValue(prereq.Lesson, out var state))
                {
                    continue; // Cạnh treo, đã báo ở E102.
                }

                if (state == Grey)
                {
                    var start = stack.IndexOf(prereq.Lesson);
                    cycles.Add([.. stack[(start < 0 ? 0 : start)..], prereq.Lesson]);
                }
                else if (state == White)
                {
                    Visit(prereq.Lesson);
                }
            }

            stack.RemoveAt(stack.Count - 1);
            colour[code] = Black;
        }

        foreach (var code in byCode.Keys.Where(c => colour[c] == White))
        {
            Visit(code);
        }

        return cycles;
    }

    private static string Trim(string value) =>
        value.Length <= 40 ? value : value[..40] + "...";

    /// <summary>
    /// Các trường văn xuôi dài mà học viên đọc nguyên văn.
    ///
    /// Chỉ liệt kê những trường hay được viết trong khối block scalar, vì đó là
    /// chỗ duy nhất dấu gạch chéo lọt được vào mà không ai để ý.
    /// </summary>
    private static IEnumerable<(string Field, string Text)> ProseFields(LessonDocument doc)
    {
        yield return ("objective_vi", doc.ObjectiveVi ?? "");
        yield return ("memory_trick_vi", doc.MemoryTrickVi ?? "");

        if (doc.Explanation is { } explanation)
        {
            yield return ("explanation.why_vi", explanation.WhyVi ?? "");
            yield return ("explanation.how_vi", explanation.HowVi ?? "");
            yield return ("explanation.contrast_vi", explanation.ContrastVi ?? "");
        }

        foreach (var mistake in doc.CommonMistakes)
        {
            yield return ($"common_mistake \"{Trim(mistake.Mistake ?? "")}\".why_vi", mistake.WhyVi ?? "");
            yield return ($"common_mistake \"{Trim(mistake.Mistake ?? "")}\".fix_vi", mistake.FixVi ?? "");
        }
    }
}
