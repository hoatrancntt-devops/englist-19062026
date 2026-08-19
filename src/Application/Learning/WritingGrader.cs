using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Learning;

/// <summary>Luật chấm một bài viết, lấy từ YAML.</summary>
public record WritingRubric(
    WritingTaskKind Kind,
    IReadOnlyList<IReadOnlyList<string>> Blanks,
    IReadOnlyList<string> CorrectOrder,
    IReadOnlyList<string> RequiredPoints,
    string SampleEn);

public record WritingGrade(double Score, bool Passed, string FeedbackVi, string SampleEn);

/// <summary>
/// Chấm bài viết bằng luật, ngay tại máy chủ.
///
/// Không dùng AI: chấm phải chạy được khi chưa cấu hình khoá API nào, và phải cho ra
/// cùng một điểm với cùng một câu trả lời — học viên không chấp nhận điểm nhảy múa.
///
/// Ba dạng, ba bộ luật khác nhau, vì ba dạng đo ba thứ khác nhau.
/// </summary>
public class WritingGrader
{
    public WritingGrade Grade(WritingRubric rubric, IReadOnlyList<string> answers, int passScore)
    {
        if (answers.Count == 0 || answers.All(string.IsNullOrWhiteSpace))
        {
            return new WritingGrade(0, false, "Bạn chưa viết gì.", rubric.SampleEn);
        }

        return rubric.Kind switch
        {
            WritingTaskKind.FillBlank => GradeFillBlank(rubric, answers, passScore),
            WritingTaskKind.Reorder => GradeReorder(rubric, answers, passScore),
            WritingTaskKind.GuidedEmail => GradeGuidedEmail(rubric, answers, passScore),
            _ => new WritingGrade(0, false, "Dạng bài viết chưa được hỗ trợ.", rubric.SampleEn),
        };
    }

    /// <summary>
    /// Điền chỗ trống. Chấp nhận sai chính tả nhẹ: mục tiêu là dùng đúng từ,
    /// không phải thi chính tả. Sai một ký tự trên từ dài không nên bị 0 điểm.
    /// </summary>
    private static WritingGrade GradeFillBlank(WritingRubric rubric, IReadOnlyList<string> answers, int passScore)
    {
        if (rubric.Blanks.Count == 0)
        {
            return new WritingGrade(0, false, "Bài này thiếu dữ liệu để chấm.", rubric.SampleEn);
        }

        var perBlank = new List<double>();
        var wrong = new List<string>();

        for (var i = 0; i < rubric.Blanks.Count; i++)
        {
            var accepted = rubric.Blanks[i];
            var given = i < answers.Count ? TextMatching.Normalize(answers[i]) : string.Empty;

            if (given.Length == 0)
            {
                perBlank.Add(0);
                wrong.Add($"chỗ trống {i + 1} bỏ trống");
                continue;
            }

            if (accepted.Any(a => TextMatching.Normalize(a) == given))
            {
                perBlank.Add(100);
                continue;
            }

            // Gần đúng: khoảng cách sửa lỗi không quá 1/4 độ dài từ.
            var best = accepted
                .Select(a => TextMatching.Normalize(a))
                .Select(a => new { Word = a, Distance = TextMatching.EditDistance(a, given) })
                .OrderBy(x => x.Distance)
                .First();

            var tolerance = Math.Max(1, best.Word.Length / 4);

            if (best.Distance <= tolerance)
            {
                perBlank.Add(80);
                wrong.Add($"chỗ trống {i + 1} gần đúng, chính tả là \"{accepted[0]}\"");
            }
            else
            {
                perBlank.Add(0);
                wrong.Add($"chỗ trống {i + 1} chưa đúng");
            }
        }

        var score = Math.Round(perBlank.Average(), 1);
        var passed = score >= passScore;

        var feedback = wrong.Count == 0
            ? "Đúng hết."
            : (passed ? "Gần được rồi. " : "Còn vài chỗ cần sửa. ") + string.Join("; ", wrong.Take(3)) + ".";

        return new WritingGrade(score, passed, feedback, rubric.SampleEn);
    }

    /// <summary>
    /// Sắp lại thứ tự. Chấm theo số cặp liền kề đúng chứ không phải đúng-sai toàn phần:
    /// đặt sai một mảnh không nên xoá sạch công sắp đúng phần còn lại.
    /// </summary>
    private static WritingGrade GradeReorder(WritingRubric rubric, IReadOnlyList<string> answers, int passScore)
    {
        if (rubric.CorrectOrder.Count < 2)
        {
            return new WritingGrade(0, false, "Bài này thiếu dữ liệu để chấm.", rubric.SampleEn);
        }

        var expected = rubric.CorrectOrder.Select(TextMatching.Normalize).ToList();
        var given = answers.Select(TextMatching.Normalize).ToList();

        if (given.Count != expected.Count)
        {
            return new WritingGrade(0, false,
                $"Cần đúng {expected.Count} mảnh, bạn xếp {given.Count}.", rubric.SampleEn);
        }

        var totalPairs = expected.Count - 1;
        var correctPairs = 0;

        for (var i = 0; i < totalPairs; i++)
        {
            var indexA = expected.IndexOf(given[i]);
            var indexB = expected.IndexOf(given[i + 1]);

            if (indexA >= 0 && indexB == indexA + 1)
            {
                correctPairs++;
            }
        }

        var score = Math.Round(correctPairs * 100.0 / totalPairs, 1);
        var exact = given.SequenceEqual(expected);

        return new WritingGrade(
            exact ? 100 : score,
            exact || score >= passScore,
            exact ? "Đúng toàn bộ thứ tự." : $"Đúng {correctPairs} trên {totalPairs} cặp liền kề.",
            rubric.SampleEn);
    }

    /// <summary>
    /// Email có hướng dẫn. Đo hai thứ: có đủ ý bắt buộc không, và có viết thành câu không.
    ///
    /// KHÔNG chấm văn phong hay ngữ pháp — chấm được hai thứ đó bằng luật là ảo tưởng,
    /// và chấm sai còn hại hơn không chấm.
    /// </summary>
    private static WritingGrade GradeGuidedEmail(WritingRubric rubric, IReadOnlyList<string> answers, int passScore)
    {
        if (rubric.RequiredPoints.Count == 0)
        {
            return new WritingGrade(0, false, "Bài này thiếu dữ liệu để chấm.", rubric.SampleEn);
        }

        var text = TextMatching.Normalize(string.Join(" ", answers));

        var missing = rubric.RequiredPoints
            .Where(point => !TextMatching.ContainsPoint(text, point))
            .ToList();

        var covered = rubric.RequiredPoints.Count - missing.Count;
        var coverageScore = covered * 100.0 / rubric.RequiredPoints.Count;

        // Đủ ý nhưng viết ba chữ rời rạc thì chưa phải một email.
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var lengthPenalty = wordCount < rubric.RequiredPoints.Count * 4 ? 20 : 0;

        var score = Math.Round(Math.Max(0, coverageScore - lengthPenalty), 1);

        var feedback = missing.Count == 0
            ? lengthPenalty > 0
                ? "Đủ ý nhưng còn quá ngắn. Viết thành câu hoàn chỉnh."
                : "Đủ ý cần có."
            : $"Thiếu {missing.Count} ý: {string.Join("; ", missing.Take(3))}.";

        return new WritingGrade(score, score >= passScore, feedback, rubric.SampleEn);
    }
}
