using System.Text.RegularExpressions;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Content;

/// <summary>
/// Cổng chất lượng bộ bài luyện viết.
///
/// Bài viết hỏng tệ hơn chương truyện hỏng: nó chấm sai điểm mà vẫn trông như đang chạy đúng.
/// Một chỗ trống thiếu đáp án thì học viên viết đúng vẫn bị 0, và họ sẽ tin là mình sai.
/// Nên mọi quy tắc ở đây đều chặn cứng.
/// </summary>
public partial class WritingValidator
{
    private const int MinTasks = 4;
    private const int MaxTasks = 10;

    [GeneratedRegex(@"^WR-[A-Z]{2,12}$")]
    private static partial Regex SetCodePattern { get; }

    public IReadOnlyList<ValidationIssue> ValidateOne(WritingSetDocument doc)
    {
        var issues = new List<ValidationIssue>();
        var code = string.IsNullOrWhiteSpace(doc.Code) ? "(chưa có mã)" : doc.Code;

        void Add(string rule, string message) => issues.Add(new ValidationIssue(rule, code, message));

        if (string.IsNullOrWhiteSpace(doc.Code))
        {
            Add("W001", "Thiếu mã bộ bài.");
        }
        else if (!SetCodePattern.IsMatch(doc.Code))
        {
            Add("W002", $"Mã {doc.Code} sai dạng. Bộ bài đặt mã kiểu WR-INF, WR-SEC.");
        }

        if (string.IsNullOrWhiteSpace(doc.TitleVi))
        {
            Add("W003", "Thiếu tiêu đề tiếng Việt.");
        }

        if (string.IsNullOrWhiteSpace(doc.ContextVi))
        {
            Add("W004", "Thiếu bối cảnh. Học viên phải biết mình đang viết cho ai, trong tình huống nào.");
        }

        if (doc.Tasks.Count < MinTasks)
        {
            Add("W005", $"Chỉ có {doc.Tasks.Count} bài. Dưới {MinTasks} thì chưa thành một bộ luyện.");
        }

        if (doc.Tasks.Count > MaxTasks)
        {
            Add("W006", $"Có {doc.Tasks.Count} bài. Trên {MaxTasks} là quá dài cho một buổi.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var task in doc.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Code))
            {
                Add("W007", "Có bài thiếu mã.");
                continue;
            }

            if (!seen.Add(task.Code))
            {
                Add("W008", $"Mã bài {task.Code} bị trùng trong cùng bộ.");
            }

            ValidateTask(task, Add);
        }

        return issues;
    }

    private static void ValidateTask(WritingSetTaskDocument task, Action<string, string> add)
    {
        var where = $"bài {task.Code}";

        if (string.IsNullOrWhiteSpace(task.PromptVi))
        {
            add("W009", $"{where}: thiếu câu lệnh tiếng Việt.");
        }

        if (task.PassScore is < 1 or > 100)
        {
            add("W010", $"{where}: pass_score {task.PassScore} nằm ngoài khoảng 1–100.");
        }

        // Câu mẫu hiện ra sau khi chấm. Thiếu nó thì học viên biết mình sai mà không biết đúng là gì.
        if (string.IsNullOrWhiteSpace(task.SampleEn))
        {
            add("W011", $"{where}: thiếu sample_en. Đây là thứ học viên đọc sau khi nộp.");
        }

        switch (task.Kind)
        {
            case WritingTaskKind.FillBlank:
                ValidateFillBlank(task, where, add);
                break;

            case WritingTaskKind.Reorder:
                ValidateReorder(task, where, add);
                break;

            case WritingTaskKind.GuidedEmail:
                ValidateGuidedEmail(task, where, add);
                break;
        }
    }

    private static void ValidateFillBlank(WritingSetTaskDocument task, string where, Action<string, string> add)
    {
        if (string.IsNullOrWhiteSpace(task.PromptEn))
        {
            add("W012", $"{where}: dạng điền chỗ trống mà không có câu tiếng Anh.");
        }

        if (task.Blanks.Count == 0)
        {
            add("W013", $"{where}: không khai chỗ trống nào.");
            return;
        }

        for (var i = 0; i < task.Blanks.Count; i++)
        {
            if (task.Blanks[i].Count == 0)
            {
                add("W014", $"{where}: chỗ trống {i + 1} không có đáp án nào. Học viên viết đúng vẫn bị chấm sai.");
            }

            if (task.Blanks[i].Any(string.IsNullOrWhiteSpace))
            {
                add("W014", $"{where}: chỗ trống {i + 1} có đáp án rỗng.");
            }
        }
    }

    private static void ValidateReorder(WritingSetTaskDocument task, string where, Action<string, string> add)
    {
        if (task.CorrectOrder.Count < 3)
        {
            add("W015", $"{where}: chỉ có {task.CorrectOrder.Count} mảnh. Dưới 3 thì sắp xếp không còn là bài tập.");
            return;
        }

        if (task.CorrectOrder.Any(string.IsNullOrWhiteSpace))
        {
            add("W016", $"{where}: correct_order có mảnh rỗng.");
        }

        if (task.Fragments.Count == 0)
        {
            add("W017", $"{where}: thiếu fragments. Không có thứ tự hiển thị thì học viên thấy luôn đáp án.");
            return;
        }

        var display = task.Fragments.Select(Normalize).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var correct = task.CorrectOrder.Select(Normalize).OrderBy(s => s, StringComparer.Ordinal).ToList();

        if (!display.SequenceEqual(correct, StringComparer.Ordinal))
        {
            add("W018", $"{where}: fragments và correct_order không phải cùng một tập mảnh.");
            return;
        }

        // Thứ tự hiển thị trùng thứ tự đúng nghĩa là đề đã bày sẵn đáp án: học viên bấm nộp
        // mà không sắp gì cũng được 100.
        if (task.Fragments.Select(Normalize).SequenceEqual(task.CorrectOrder.Select(Normalize), StringComparer.Ordinal))
        {
            add("W019", $"{where}: fragments đang xếp đúng sẵn thứ tự đáp án. Xáo lại.");
        }
    }

    private static void ValidateGuidedEmail(WritingSetTaskDocument task, string where, Action<string, string> add)
    {
        if (task.RequiredPoints.Count < 2)
        {
            add("W020", $"{where}: chỉ có {task.RequiredPoints.Count} ý bắt buộc. Cần ít nhất 2 để chấm có nghĩa.");
        }

        if (task.RequiredPoints.Any(string.IsNullOrWhiteSpace))
        {
            add("W021", $"{where}: có ý bắt buộc rỗng.");
        }
    }

    public IReadOnlyList<ValidationIssue> ValidateSet(IReadOnlyCollection<WritingSetDocument> docs)
    {
        var issues = docs.SelectMany(ValidateOne).ToList();

        foreach (var group in docs
            .Where(d => !string.IsNullOrWhiteSpace(d.Code))
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            issues.Add(new ValidationIssue("W030", group.Key, $"Mã bộ bài lặp {group.Count()} lần."));
        }

        return issues;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
