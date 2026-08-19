using System.Text.RegularExpressions;

namespace EnglishForIT.Application.Content;

/// <summary>
/// Cổng chất lượng chương truyện.
///
/// Truyện không chấm điểm nên hỏng ở đây không làm sai kết quả học. Nhưng nó hỏng theo kiểu
/// khác và khó thấy hơn: hai chương cùng mốc mở thì bật ra một lúc rồi im bặt hàng chục bài;
/// một chương trỏ vào mã bài không tồn tại thì khoá vĩnh viễn mà không báo gì.
/// Cả hai đều chỉ lộ ra sau khi học viên đã học được vài tuần, nên chặn từ lúc seed.
/// </summary>
public partial class StoryValidator
{
    /// <summary>Độ dài thân chương. Dưới ngưỡng dưới là ghi chú chứ chưa thành chương.</summary>
    private const int MinBodyChars = 300;

    /// <summary>Trên ngưỡng trên là bài đọc, không còn là quãng nghỉ giữa hai bài học.</summary>
    private const int MaxBodyChars = 3500;

    [GeneratedRegex(@"^ST-\d{2}$")]
    private static partial Regex ChapterCodePattern { get; }

    [GeneratedRegex(@"^[A-Z]+-\d{2}$")]
    private static partial Regex LessonCodePattern { get; }

    public IReadOnlyList<ValidationIssue> ValidateOne(StoryDocument doc)
    {
        var issues = new List<ValidationIssue>();
        var code = string.IsNullOrWhiteSpace(doc.Code) ? "(chưa có mã)" : doc.Code;

        void Add(string rule, string message) => issues.Add(new ValidationIssue(rule, code, message));

        if (string.IsNullOrWhiteSpace(doc.Code))
        {
            Add("S001", "Thiếu mã chương.");
        }
        else if (!ChapterCodePattern.IsMatch(doc.Code))
        {
            Add("S002", $"Mã {doc.Code} sai dạng. Chương đặt mã ST-01 tới ST-99.");
        }

        if (string.IsNullOrWhiteSpace(doc.TitleVi))
        {
            Add("S003", "Thiếu tiêu đề tiếng Việt.");
        }
        else if (doc.TitleVi.Length > 300)
        {
            Add("S003", $"Tiêu đề dài {doc.TitleVi.Length} ký tự, cột chỉ chứa 300.");
        }

        if (doc.Number < 1)
        {
            Add("S004", "Thiếu số thứ tự chương, hoặc số nhỏ hơn 1.");
        }

        if (string.IsNullOrWhiteSpace(doc.HookVi))
        {
            Add("S005", "Thiếu câu mở chương. Đây là thứ duy nhất học viên thấy khi chương còn khoá.");
        }
        else if (doc.HookVi.Length > 500)
        {
            Add("S005", $"Câu mở dài {doc.HookVi.Length} ký tự, cột chỉ chứa 500.");
        }

        var bodyLength = doc.BodyVi?.Trim().Length ?? 0;

        if (bodyLength == 0)
        {
            Add("S006", "Thiếu thân chương.");
        }
        else if (bodyLength < MinBodyChars)
        {
            Add("S006", $"Thân chương chỉ {bodyLength} ký tự. Dưới {MinBodyChars} thì chưa thành chương.");
        }
        else if (bodyLength > MaxBodyChars)
        {
            Add("S006", $"Thân chương {bodyLength} ký tự. Trên {MaxBodyChars} là bài đọc chứ không còn là quãng nghỉ.");
        }

        if (string.IsNullOrWhiteSpace(doc.EndsVi))
        {
            Add("S007", "Thiếu câu kết. Chương phải nối sang việc học kế tiếp, không được cụt.");
        }

        if (string.IsNullOrWhiteSpace(doc.UnlockAfterLesson))
        {
            Add("S008", "Thiếu unlock_after_lesson. Không có mốc thì chương không bao giờ mở.");
        }
        else if (!LessonCodePattern.IsMatch(doc.UnlockAfterLesson))
        {
            Add("S009", $"Mốc mở {doc.UnlockAfterLesson} sai dạng mã bài, ví dụ đúng: LIFE-04.");
        }

        foreach (var character in doc.NewCharacters)
        {
            if (string.IsNullOrWhiteSpace(character))
            {
                Add("S010", "Có mục nhân vật rỗng.");
            }
            else if (!character.Contains('—'))
            {
                Add("S010",
                    $"Nhân vật \"{character}\" thiếu vai. Viết dạng \"Tên — vai\" để học viên biết người này là ai.");
            }
        }

        return issues;
    }

    /// <summary>
    /// Kiểm cả tập chương. <paramref name="knownLessonCodes"/> là mã bài đang có thật;
    /// truyền null khi chưa biết (ví dụ kiểm nhanh một file lẻ) thì bỏ qua phép tra mốc.
    /// </summary>
    public IReadOnlyList<ValidationIssue> ValidateSet(
        IReadOnlyCollection<StoryDocument> docs,
        IReadOnlySet<string>? knownLessonCodes = null)
    {
        var issues = docs.SelectMany(ValidateOne).ToList();

        void Add(string rule, string code, string message) => issues.Add(new ValidationIssue(rule, code, message));

        foreach (var group in docs
            .Where(d => !string.IsNullOrWhiteSpace(d.Code))
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            Add("S020", group.Key, $"Mã chương lặp {group.Count()} lần.");
        }

        foreach (var group in docs.GroupBy(d => d.Number).Where(g => g.Count() > 1))
        {
            Add("S021", string.Join(", ", group.Select(d => d.Code)),
                $"Số thứ tự {group.Key} bị nhiều chương dùng chung. Cột number có ràng buộc duy nhất.");
        }

        // Số phải liền mạch từ 1: thiếu một số ở giữa nghĩa là có chương chưa soạn, và
        // danh sách chương sẽ hiện một khoảng trống không giải thích được.
        var numbers = docs.Select(d => d.Number).Where(n => n >= 1).Distinct().OrderBy(n => n).ToList();

        if (numbers.Count > 0 && numbers[^1] != numbers.Count)
        {
            var missing = Enumerable.Range(1, numbers[^1]).Except(numbers).ToList();

            if (missing.Count > 0)
            {
                Add("S022", "(cả tập)", $"Thiếu chương số {string.Join(", ", missing)}. Số chương phải liền mạch từ 1.");
            }
        }

        foreach (var group in docs
            .Where(d => !string.IsNullOrWhiteSpace(d.UnlockAfterLesson))
            .GroupBy(d => d.UnlockAfterLesson, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            Add("S023", string.Join(", ", group.Select(d => d.Code)),
                $"Cùng mở tại {group.Key}. Hai chương bật một lúc rồi im bặt là lỗi nhịp, tách mốc ra.");
        }

        if (knownLessonCodes is not null)
        {
            foreach (var doc in docs.Where(d => !string.IsNullOrWhiteSpace(d.UnlockAfterLesson)))
            {
                if (!knownLessonCodes.Contains(doc.UnlockAfterLesson))
                {
                    Add("S024", doc.Code,
                        $"Mốc mở trỏ tới bài {doc.UnlockAfterLesson} nhưng không có bài nào mang mã đó. Chương sẽ khoá vĩnh viễn.");
                }
            }
        }

        return issues;
    }
}
