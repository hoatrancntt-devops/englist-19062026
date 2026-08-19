using System.Text.Json;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Learning;

/// <summary>Một câu đã chấm, kèm đủ thông tin để học viên biết mình sai ở đâu.</summary>
public record GradedItem(string ItemCode, bool Correct, int ChosenIndex, int CorrectIndex);

public record ActivityGrade(
    double Score,
    bool Passed,
    IReadOnlyList<GradedItem> Items,
    string FeedbackVi,
    /// <summary>
    /// False khi hệ thống chưa chấm được bước này (hiện là phần Nói, chờ dịch vụ nhận dạng
    /// giọng nói ở giai đoạn sau). Bước chưa chấm được KHÔNG tính vào mastery —
    /// cho điểm 100 vì không chấm nổi là nói dối học viên rằng họ đã thạo.
    /// </summary>
    bool Graded = true,
    string? SampleEn = null);

/// <summary>Câu trả lời thô từ client: mã câu và chỉ số lựa chọn.</summary>
public record ItemResponse(string ItemCode, int ChosenIndex);

/// <summary>
/// Chấm các bước trắc nghiệm.
///
/// Toàn bộ việc chấm chạy phía máy chủ. Client gửi lên chỉ số lựa chọn, không bao giờ
/// nhận được đáp án trước khi nộp — đây là ràng buộc kiến trúc, không phải khuyến nghị.
/// </summary>
public class ActivityGrader
{
    /// <summary>
    /// Chấm một bước trắc nghiệm.
    /// </summary>
    /// <param name="answerKeyByCode">Mã câu và JSON đáp án lấy thẳng từ DB.</param>
    public ActivityGrade GradeMultipleChoice(
        IReadOnlyList<ItemResponse> responses,
        IReadOnlyDictionary<string, string> answerKeyByCode,
        int passScore)
    {
        if (answerKeyByCode.Count == 0)
        {
            // Bước từ vựng không có gì để chấm — xem xong là xong. Đây là đánh giá thật
            // chứ không phải điểm bịa: mục tiêu của bước này đúng là tiếp xúc với từ mới.
            return new ActivityGrade(100, true, [], "Đã xem xong.");
        }

        var graded = new List<GradedItem>();

        foreach (var (code, answerJson) in answerKeyByCode)
        {
            var correctIndex = ReadCorrectIndex(answerJson);
            var chosen = responses.FirstOrDefault(r => r.ItemCode == code)?.ChosenIndex ?? -1;

            graded.Add(new GradedItem(code, chosen == correctIndex, chosen, correctIndex));
        }

        var correctCount = graded.Count(g => g.Correct);
        var score = Math.Round(correctCount * 100.0 / graded.Count, 1);

        return new ActivityGrade(
            score,
            score >= passScore,
            graded,
            BuildFeedback(correctCount, graded.Count, score >= passScore));
    }

    /// <summary>
    /// Gộp điểm các bước thành điểm từng trục kỹ năng.
    ///
    /// Một kỹ năng có nhiều bước thì lấy trung bình. Bước chưa làm không tính vào,
    /// để học viên làm dở nửa bài không bị tụt điểm trục đó xuống 0.
    /// </summary>
    public Dictionary<SkillType, double> AggregateSkillScores(
        IReadOnlyList<(SkillType Skill, double Score)> activityScores)
    {
        return activityScores
            .GroupBy(a => a.Skill)
            .ToDictionary(g => g.Key, g => Math.Round(g.Average(a => a.Score), 1));
    }

    /// <summary>
    /// Đọc chỉ số đáp án đúng.
    ///
    /// Tra tên thuộc tính KHÔNG phân biệt hoa thường, vì JsonDocument phân biệt hoa thường
    /// còn bộ ghi dùng PascalCase. Lệch một chữ hoa từng khiến mọi câu đều bị chấm sai
    /// mà không có lỗi nào được ném ra — kiểu hỏng im lặng tệ nhất.
    /// </summary>
    private static int ReadCorrectIndex(string answerJson)
    {
        using var doc = JsonDocument.Parse(answerJson);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.NameEquals("answer")
                || string.Equals(property.Name, "answer", StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.TryGetInt32(out var index) ? index : -1;
            }
        }

        return -1;
    }

    private static string BuildFeedback(int correct, int total, bool passed)
    {
        if (passed && correct == total)
        {
            return $"Đúng cả {total} câu.";
        }

        if (passed)
        {
            return $"Đúng {correct} trên {total} câu. Đủ để đi tiếp.";
        }

        return $"Đúng {correct} trên {total} câu. Chưa đủ — xem lại phần giải thích rồi thử lại.";
    }
}
