using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Learning;

/// <summary>Câu trả lời đã chấm xong, đủ dữ liệu để tổng hợp thành kết quả.</summary>
public record ScoredItem(
    string ItemCode,
    PlacementItemKind Kind,
    SkillType? Skill,
    double Weight,
    double Score,
    bool AnsweredFast,
    /// <summary>Chỉ số lựa chọn của câu Likert có bật self_rating. Null với mọi câu khác.</summary>
    int? SelfRatingIndex,
    /// <summary>Số lựa chọn của câu Likert đó, để quy ra thang 0-100.</summary>
    int? SelfRatingChoiceCount);

public record PlacementOutcome(
    /// <summary>Bậc hiển thị cho học viên: L0 tới L4.</summary>
    string Band,

    /// <summary>Bậc engine dùng để mở khoá. L0 và L1 cùng là PreA1 — enum không có gì thấp hơn.</summary>
    CefrLevel Level,

    ContextLayer SuggestedLayer,
    Dictionary<SkillType, double> SkillScores,
    double VocabGrammarScore,
    double OverallScore,
    double FastAnswerRatio,
    double SelfRatedScore,

    /// <summary>Kỹ năng không có câu nào chấm được. Phải hiện rõ, không được ngầm cho 0 điểm.</summary>
    IReadOnlyList<SkillType> UnmeasuredSkills,

    IReadOnlyList<string> NotesVi);

/// <summary>
/// Chấm đề xếp lớp và quy ra bậc.
///
/// Thuần tính toán, không chạm DB — đặt ở đây để test được từng luật riêng lẻ,
/// vì đây là chỗ sai thì cả lộ trình phía sau đặt nhầm mà không ai thấy ngay.
/// </summary>
public static class PlacementScoring
{
    /// <summary>Trả lời nhanh hơn ngưỡng này thì coi là đoán mò.</summary>
    private const double FastAnswerFraction = 0.15;

    /// <summary>Dưới ngưỡng này không đủ nhanh để kết luận gì, dù câu có ngưỡng chậm rất lớn.</summary>
    private const int MinFastAnswerSeconds = 3;

    /// <summary>Tỷ lệ đoán mò vượt mức này thì kết quả kèm cảnh báo.</summary>
    private const double GuessWarningRatio = 0.4;

    /// <summary>Lệch giữa tự đánh giá và điểm thật vượt mức này thì nói ra.</summary>
    private const double SelfRatingGapWarning = 25;

    // ---------------------------------------------------------------
    // Chấm một câu
    // ---------------------------------------------------------------

    /// <summary>Câu trả lời thô của học viên, đã tách khỏi JSON.</summary>
    public record Response(int? ChoiceIndex, string? Text);

    /// <summary>
    /// Chấm một câu, thang 0-100.
    ///
    /// Câu Likert và câu nói trả về null: hai loại này không có đáp án đúng nên
    /// không được lẫn vào điểm trục kỹ năng. Gọi hàm này rồi ép về 0 khi nhận null
    /// là cách nhanh nhất để kéo trục Nói của mọi học viên xuống đáy.
    /// </summary>
    public static double? Grade(PlacementItemKind kind, PlacementAnswerDocument answer, Response response)
    {
        switch (kind)
        {
            case PlacementItemKind.Likert:
            case PlacementItemKind.ReadAloud:
            case PlacementItemKind.Repeat:
                return null;

            case PlacementItemKind.Mcq:
            case PlacementItemKind.McqRead:
                if (response.ChoiceIndex is null || answer.CorrectIndex is null)
                {
                    return 0;
                }

                return response.ChoiceIndex == answer.CorrectIndex ? 100 : 0;

            case PlacementItemKind.FillBlank:
                return GradeAccepted(answer, response.Text, allowNearMiss: true);

            case PlacementItemKind.ErrorCorrection:
            case PlacementItemKind.ShortAnswer:
                return GradeAccepted(answer, response.Text, allowNearMiss: false);

            case PlacementItemKind.GuidedEmail:
                return GradeGuidedEmail(answer, response.Text);

            default:
                return 0;
        }
    }

    /// <summary>
    /// So với danh sách cách viết được chấp nhận.
    ///
    /// Điền chỗ trống cho phép gõ sai nhẹ vì nó đo việc chọn đúng từ, không đo chính tả.
    /// Sửa lỗi và trả lời ngắn thì không: ở đó viết đúng cả câu chính là thứ đang được đo.
    /// </summary>
    private static double GradeAccepted(PlacementAnswerDocument answer, string? text, bool allowNearMiss)
    {
        if (string.IsNullOrWhiteSpace(text) || answer.Accepted is not { Count: > 0 })
        {
            return 0;
        }

        var given = TextMatching.Normalize(text);

        if (given.Length == 0)
        {
            return 0;
        }

        var accepted = answer.Accepted.Select(TextMatching.Normalize).ToList();

        if (accepted.Contains(given, StringComparer.Ordinal))
        {
            return 100;
        }

        if (!allowNearMiss)
        {
            return 0;
        }

        var best = accepted
            .Select(a => new { Word = a, Distance = TextMatching.EditDistance(a, given) })
            .OrderBy(x => x.Distance)
            .First();

        var tolerance = Math.Max(1, best.Word.Length / 4);

        return best.Distance <= tolerance ? 80 : 0;
    }

    /// <summary>
    /// Email có hướng dẫn: đo đủ ý và đủ dài, không đo văn phong.
    ///
    /// Cùng nguyên tắc với <see cref="WritingGrader"/> — chấm ngữ pháp bằng luật
    /// là ảo tưởng, và chấm sai hại hơn không chấm.
    /// </summary>
    private static double GradeGuidedEmail(PlacementAnswerDocument answer, string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || answer.MustContain is not { Count: > 0 })
        {
            return 0;
        }

        var normalized = TextMatching.Normalize(text);
        var covered = answer.MustContain.Count(p => TextMatching.ContainsPoint(normalized, p));
        var coverage = covered * 100.0 / answer.MustContain.Count;

        var minWords = answer.MinWords ?? 0;

        if (minWords <= 0)
        {
            return Math.Round(coverage, 1);
        }

        var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        if (wordCount >= minWords)
        {
            return Math.Round(coverage, 1);
        }

        // Viết đủ ý nhưng quá ngắn thì trừ theo tỷ lệ thiếu chứ không cho 0:
        // ba ý gói trong mười từ vẫn hơn hẳn một email không có ý nào.
        return Math.Round(coverage * wordCount / minWords, 1);
    }

    /// <summary>Trả lời nhanh tới mức không kịp đọc đề.</summary>
    public static bool IsFastAnswer(int responseSeconds, int slowAnswerSeconds)
    {
        var threshold = Math.Max(MinFastAnswerSeconds, (int)(slowAnswerSeconds * FastAnswerFraction));
        return responseSeconds < threshold;
    }

    // ---------------------------------------------------------------
    // Tổng hợp cả lượt thi
    // ---------------------------------------------------------------

    public static PlacementOutcome Summarise(IReadOnlyList<ScoredItem> items)
    {
        var notes = new List<string>();

        var skillScores = new Dictionary<SkillType, double>();
        var unmeasured = new List<SkillType>();

        foreach (var skill in Enum.GetValues<SkillType>())
        {
            var forSkill = items.Where(i => i.Skill == skill && IsScorable(i.Kind)).ToList();

            if (forSkill.Count == 0)
            {
                unmeasured.Add(skill);
                continue;
            }

            skillScores[skill] = WeightedAverage(forSkill);
        }

        var vocabGrammarItems = items
            .Where(i => i.Skill is null && IsScorable(i.Kind))
            .ToList();

        var vocabGrammar = vocabGrammarItems.Count > 0 ? WeightedAverage(vocabGrammarItems) : 0;

        var overall = skillScores.Count > 0
            ? Math.Round(skillScores.Values.Average(), 1)
            : 0;

        var answered = items.Where(i => IsScorable(i.Kind)).ToList();
        var fastRatio = answered.Count > 0
            ? Math.Round(answered.Count(i => i.AnsweredFast) * 1.0 / answered.Count, 3)
            : 0;

        var selfRated = SelfRatedScore(items);

        // --- Quy ra bậc ---
        var overallBand = BandOf(overall);

        // Điểm tổng cao không được che trục yếu. Người đọc tốt mà nghe không nổi
        // thì đặt vào bậc cao sẽ gặp bài đầu tiên đã không theo được, và bỏ luôn.
        // Chỉ xét trong các trục ĐO ĐƯỢC — trục chưa đo không phải trục yếu.
        var band = overallBand;

        if (skillScores.Count > 0)
        {
            var weakest = skillScores.Values.Min();
            var cap = BandOf(weakest) + 1;

            if (cap < band)
            {
                band = cap;
                var weakestSkill = skillScores.OrderBy(kv => kv.Value).First();
                notes.Add(
                    $"Điểm chung của bạn ở mức L{overallBand}, nhưng trục {SkillNameVi(weakestSkill.Key)} " +
                    $"chỉ được {weakestSkill.Value:0} điểm nên xếp L{band}. Học từ đây lên sẽ chắc hơn.");
            }
        }

        if (unmeasured.Count > 0)
        {
            notes.Add(
                $"Chưa đo được trục {string.Join(" và ", unmeasured.Select(SkillNameVi))} " +
                "vì phần chấm phát âm chưa hoạt động. Bậc trên tính từ các trục còn lại.");
        }

        if (fastRatio > GuessWarningRatio)
        {
            notes.Add(
                $"Bạn trả lời rất nhanh ở {fastRatio * 100:0}% số câu. Nếu có đoán bừa thì kết quả này " +
                "thấp hơn hoặc cao hơn thực lực. Thi lại bằng đề còn lại lúc nào cũng được.");
        }

        if (selfRated > 0 && Math.Abs(selfRated - overall) > SelfRatingGapWarning)
        {
            notes.Add(selfRated > overall
                ? "Bạn tự đánh giá cao hơn kết quả chấm khá nhiều. Phần lớn là do nghe và nói khó hơn đọc."
                : "Bạn làm tốt hơn mình nghĩ. Kết quả chấm cao hơn phần bạn tự đánh giá.");
        }

        return new PlacementOutcome(
            Band: $"L{band}",
            Level: LevelOf(band),
            SuggestedLayer: LayerOf(band),
            SkillScores: skillScores,
            VocabGrammarScore: vocabGrammar,
            OverallScore: overall,
            FastAnswerRatio: fastRatio,
            SelfRatedScore: selfRated,
            UnmeasuredSkills: unmeasured,
            NotesVi: notes);
    }

    /// <summary>Câu có đáp án đúng. Likert và câu nói không tính vào điểm nào.</summary>
    private static bool IsScorable(PlacementItemKind kind) =>
        kind is not (PlacementItemKind.Likert or PlacementItemKind.ReadAloud or PlacementItemKind.Repeat);

    private static double WeightedAverage(IReadOnlyList<ScoredItem> items)
    {
        var totalWeight = items.Sum(i => i.Weight);

        return totalWeight <= 0
            ? 0
            : Math.Round(items.Sum(i => i.Score * i.Weight) / totalWeight, 1);
    }

    /// <summary>Quy các câu Likert có bật self_rating về thang 0-100.</summary>
    private static double SelfRatedScore(IReadOnlyList<ScoredItem> items)
    {
        var rated = items
            .Where(i => i.SelfRatingIndex is not null && i.SelfRatingChoiceCount > 1)
            .ToList();

        if (rated.Count == 0)
        {
            return 0;
        }

        var values = rated.Select(i => i.SelfRatingIndex!.Value * 100.0 / (i.SelfRatingChoiceCount!.Value - 1));
        return Math.Round(values.Average(), 1);
    }

    private static int BandOf(double score) => score switch
    {
        < 20 => 0,
        < 40 => 1,
        < 60 => 2,
        < 80 => 3,
        _ => 4,
    };

    /// <summary>
    /// L0 và L1 cùng ánh xạ về PreA1: enum CefrLevel không có mức nào thấp hơn.
    /// Khác biệt giữa hai bậc nằm ở tầng đề xuất và ở câu giải thích, không ở đây.
    /// </summary>
    private static CefrLevel LevelOf(int band) => band switch
    {
        0 or 1 => CefrLevel.PreA1,
        2 => CefrLevel.A1,
        3 => CefrLevel.A2,
        _ => CefrLevel.B1,
    };

    private static ContextLayer LayerOf(int band) => band switch
    {
        0 or 1 or 2 => ContextLayer.Life,
        3 => ContextLayer.Office,
        _ => ContextLayer.Professional,
    };

    private static string SkillNameVi(SkillType skill) => skill switch
    {
        SkillType.Listening => "Nghe",
        SkillType.Speaking => "Nói",
        SkillType.Reading => "Đọc",
        _ => "Viết",
    };
}
