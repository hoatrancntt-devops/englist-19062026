using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Content;

/// <summary>
/// Cổng chất lượng cho đề xếp lớp.
///
/// Đề xếp lớp sai nguy hiểm hơn bài học sai: bài học sai thì học viên gặp một buổi
/// khó chịu, đề sai thì cả lộ trình phía sau đặt nhầm chỗ. Nên mọi quy tắc ở đây
/// đều chặn cứng, không có cảnh báo mềm.
/// </summary>
public class PlacementValidator
{
    /// <summary>Đề chuẩn 26 câu. Lệch số này là soạn thiếu hoặc soạn thừa, không phải biến thể.</summary>
    public const int RequiredItemCount = 26;

    /// <summary>Số câu tối thiểu cho mỗi trục kỹ năng. Dưới ba câu thì điểm trục là nhiễu, không phải phép đo.</summary>
    private const int MinItemsPerSkill = 3;

    /// <summary>Cần ít nhất hai câu Likert: một cho tự tin chung, một cho kỹ năng học viên tự thấy yếu nhất.</summary>
    private const int MinLikertItems = 2;

    private const int MinVocabGrammarItems = 2;

    public IReadOnlyList<ValidationIssue> ValidateOne(PlacementDocument doc)
    {
        var issues = new List<ValidationIssue>();
        var formCode = string.IsNullOrWhiteSpace(doc.Code) ? "(thieu-ma)" : doc.Code;

        void Add(string ruleCode, string message) => issues.Add(new ValidationIssue(ruleCode, formCode, message));

        if (string.IsNullOrWhiteSpace(doc.Code))
        {
            Add("P001", "Thiếu code.");
        }

        if (string.IsNullOrWhiteSpace(doc.TitleVi))
        {
            Add("P002", "Thiếu title_vi.");
        }

        if (doc.Items.Count != RequiredItemCount)
        {
            Add("P003", $"Có {doc.Items.Count} câu, đề phải đúng {RequiredItemCount} câu. " +
                        "Hai đề song song lệch số câu thì điểm hai đề không so được với nhau.");
        }

        var duplicates = doc.Items
            .GroupBy(i => i.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            Add("P004", $"Mã câu \"{duplicate}\" lặp lại. Seeder upsert theo mã nên câu sau sẽ đè câu trước.");
        }

        foreach (var item in doc.Items)
        {
            ValidateItem(doc, item, Add);
        }

        // --- Cân đối đề ---
        foreach (var skill in Enum.GetValues<SkillType>())
        {
            var count = doc.Items.Count(i => i.Skill == skill);
            if (count < MinItemsPerSkill)
            {
                Add("P020", $"Trục {skill} chỉ có {count} câu, cần tối thiểu {MinItemsPerSkill}. " +
                            "Ít hơn thì một câu sai kéo cả trục xuống và học viên bị đặt sai bậc.");
            }
        }

        var likert = doc.Items.Count(i => i.Kind == PlacementItemKind.Likert);
        if (likert < MinLikertItems)
        {
            Add("P021", $"Chỉ có {likert} câu Likert, cần tối thiểu {MinLikertItems}. " +
                        "Không có tự đánh giá thì không đối chiếu được với điểm thật để phát hiện lệch.");
        }

        if (!doc.Items.Any(i => i.Kind == PlacementItemKind.Likert && i.SelfRating))
        {
            Add("P024", "Không có câu Likert nào bật self_rating. " +
                        "Trục tự đánh giá sẽ luôn bằng 0 và phần đối chiếu lệch mất tác dụng.");
        }

        var vocabGrammar = doc.Items.Count(i => i.Skill is null && i.Kind != PlacementItemKind.Likert);
        if (vocabGrammar < MinVocabGrammarItems)
        {
            Add("P022", $"Chỉ có {vocabGrammar} câu từ vựng–ngữ pháp, cần tối thiểu {MinVocabGrammarItems}.");
        }

        // Đáp án đúng dồn về một vị trí là lỗ hổng chết người của đề trắc nghiệm:
        // học viên cứ chọn ô đó là được điểm tuyệt đối mà không cần biết tiếng Anh,
        // và không có con số nào trong kết quả cho thấy điều đó đã xảy ra.
        var correctIndexes = doc.Items
            .Where(i => i.Answer?.CorrectIndex is not null)
            .Select(i => i.Answer!.CorrectIndex!.Value)
            .ToList();

        if (correctIndexes.Count >= 4)
        {
            var mostCommon = correctIndexes.GroupBy(x => x).OrderByDescending(g => g.Count()).First();

            if (mostCommon.Count() * 2 > correctIndexes.Count)
            {
                Add("P025", $"{mostCommon.Count()} trên {correctIndexes.Count} câu trắc nghiệm có đáp án đúng " +
                            $"ở vị trí {mostCommon.Key}. Chọn mãi một ô là qua được đề. Trải đều đáp án ra các vị trí.");
            }
        }

        // Đề toàn câu dễ thì người giỏi đụng trần và tất cả đều ra L4; đề toàn câu khó
        // thì người mất gốc sàn hết và tất cả đều ra L0. Cả hai trường hợp đề vô dụng.
        var scored = doc.Items.Where(i => i.Kind != PlacementItemKind.Likert).ToList();
        if (scored.Count > 0)
        {
            var levels = scored.Select(i => i.Difficulty).Distinct().Count();
            if (levels < 3)
            {
                Add("P023", $"Chỉ có {levels} mức độ khó trong đề, cần tối thiểu 3. " +
                            "Đề một mức độ khó không phân biệt được người ở hai bậc liền kề.");
            }
        }

        return issues;
    }

    /// <summary>Các quy tắc cần nhìn cả tập đề, không kiểm được khi đứng riêng một file.</summary>
    public IReadOnlyList<ValidationIssue> ValidateSet(IReadOnlyList<PlacementDocument> docs)
    {
        var issues = new List<ValidationIssue>();

        var duplicateForms = docs
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var code in duplicateForms)
        {
            issues.Add(new ValidationIssue("P030", code, "Hai file cùng mã đề."));
        }

        // Chống thi lại gặp đúng đề cũ chỉ hoạt động khi có từ hai đề trở lên.
        // Một đề duy nhất thì người thi lần hai gặp nguyên đề lần một và điểm tăng
        // vì nhớ đáp án, không phải vì giỏi lên.
        var active = docs.Count(d => d.IsActive);
        if (active < 2)
        {
            issues.Add(new ValidationIssue("P031", "(tap-de)",
                $"Chỉ có {active} đề đang bật. Cần tối thiểu 2 đề song song để người thi lại không gặp đúng đề cũ."));
        }

        return issues;
    }

    private static void ValidateItem(PlacementDocument doc, PlacementItemDocument item, Action<string, string> add)
    {
        var label = string.IsNullOrWhiteSpace(item.Code) ? "(thieu-ma-cau)" : item.Code;

        if (string.IsNullOrWhiteSpace(item.Code))
        {
            add("P005", "Có câu thiếu code.");
        }

        if (string.IsNullOrWhiteSpace(item.Prompt.InstructionVi))
        {
            add("P006", $"Câu {label} thiếu prompt.instruction_vi. " +
                        "Học viên mất gốc không đoán được phải làm gì nếu đề bài không bằng tiếng Việt.");
        }

        if (item.Weight <= 0)
        {
            add("P007", $"Câu {label} có weight {item.Weight}, phải lớn hơn 0.");
        }

        if (item.Difficulty is < 1 or > 5)
        {
            add("P008", $"Câu {label} có difficulty {item.Difficulty}, phải nằm trong 1-5.");
        }

        // Câu Likert đo cảm nhận, không đo kỹ năng. Gán trục cho nó sẽ khiến điểm
        // tự khai lẫn vào điểm chấm thật — đúng thứ mà trục phụ tự đánh giá sinh ra để tránh.
        if (item.Kind == PlacementItemKind.Likert && item.Skill is not null)
        {
            add("P009", $"Câu Likert {label} không được gán skill. Tự đánh giá không phải điểm kỹ năng.");
        }

        switch (item.Kind)
        {
            case PlacementItemKind.Mcq:
            case PlacementItemKind.McqRead:
                RequireChoices(item, label, add);
                RequireCorrectIndex(item, label, add);

                if (item.Kind == PlacementItemKind.McqRead && string.IsNullOrWhiteSpace(item.Prompt.PassageEn))
                {
                    add("P012", $"Câu {label} là McqRead nhưng thiếu prompt.passage_en.");
                }

                if (item.Skill == SkillType.Listening && string.IsNullOrWhiteSpace(item.Prompt.AudioText))
                {
                    add("P013", $"Câu {label} đo Listening nhưng thiếu prompt.audio_text — không có gì để phát.");
                }

                break;

            case PlacementItemKind.Likert:
                RequireChoices(item, label, add);

                if (item.Answer is not null)
                {
                    add("P014", $"Câu Likert {label} không được có answer. Tự đánh giá không có đáp án đúng.");
                }

                break;

            case PlacementItemKind.ReadAloud:
            case PlacementItemKind.Repeat:
                if (string.IsNullOrWhiteSpace(item.Prompt.TargetEn))
                {
                    add("P015", $"Câu {label} là câu nói nhưng thiếu prompt.target_en.");
                }

                if (item.Skill != SkillType.Speaking)
                {
                    add("P016", $"Câu {label} là câu nói nhưng skill không phải Speaking.");
                }

                break;

            case PlacementItemKind.FillBlank:
                if (string.IsNullOrWhiteSpace(item.Prompt.SentenceEn))
                {
                    add("P017", $"Câu {label} thiếu prompt.sentence_en.");
                }
                else if (!item.Prompt.SentenceEn.Contains("___", StringComparison.Ordinal))
                {
                    add("P017", $"Câu {label} là FillBlank nhưng sentence_en không có chỗ trống \"___\".");
                }

                RequireAccepted(item, label, add);
                break;

            case PlacementItemKind.ErrorCorrection:
                if (string.IsNullOrWhiteSpace(item.Prompt.SentenceEn))
                {
                    add("P017", $"Câu {label} thiếu prompt.sentence_en.");
                }

                RequireAccepted(item, label, add);
                break;

            case PlacementItemKind.ShortAnswer:
                RequireAccepted(item, label, add);
                break;

            case PlacementItemKind.GuidedEmail:
                if (string.IsNullOrWhiteSpace(item.Prompt.ScenarioVi))
                {
                    add("P018", $"Câu {label} là GuidedEmail nhưng thiếu prompt.scenario_vi.");
                }

                if (item.Prompt.RequiredPointsVi is not { Count: > 0 })
                {
                    add("P018", $"Câu {label} thiếu prompt.required_points_vi. " +
                                "Không nói rõ phải viết ý gì thì chấm theo từ khoá là bẫy học viên.");
                }

                if (item.Answer?.MustContain is not { Count: > 0 })
                {
                    add("P019", $"Câu {label} thiếu answer.must_contain — không có gì để chấm.");
                }
                else if (item.Prompt.RequiredPointsVi is { Count: > 0 } points &&
                         points.Count != item.Answer.MustContain.Count)
                {
                    // Học viên viết đủ số ý được yêu cầu mà vẫn mất điểm vì bộ chấm
                    // dò một số từ khoá khác — lỗi này người soạn không tự thấy được.
                    add("P019", $"Câu {label} yêu cầu {points.Count} ý nhưng chấm theo " +
                                $"{item.Answer.MustContain.Count} từ khoá. Hai con số phải khớp.");
                }

                break;
        }
    }

    private static void RequireChoices(PlacementItemDocument item, string label, Action<string, string> add)
    {
        if (item.Prompt.Choices is not { Count: >= 2 })
        {
            add("P010", $"Câu {label} cần tối thiểu 2 lựa chọn trong prompt.choices.");
        }
    }

    private static void RequireCorrectIndex(PlacementItemDocument item, string label, Action<string, string> add)
    {
        var count = item.Prompt.Choices?.Count ?? 0;
        var index = item.Answer?.CorrectIndex;

        if (index is null)
        {
            add("P011", $"Câu {label} thiếu answer.correct_index.");
            return;
        }

        if (index < 0 || index >= count)
        {
            add("P011", $"Câu {label} có correct_index {index} nằm ngoài {count} lựa chọn.");
        }
    }

    private static void RequireAccepted(PlacementItemDocument item, string label, Action<string, string> add)
    {
        if (item.Answer?.Accepted is not { Count: > 0 })
        {
            add("P011", $"Câu {label} thiếu answer.accepted — không có gì để so khớp.");
        }
    }
}
