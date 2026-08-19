namespace EnglishForIT.Application.Content;

/// <summary>
/// Cổng chất lượng kịch bản roleplay.
///
/// Kịch bản hỏng nguy hiểm hơn bài học hỏng: học viên đang giữa cuộc hội thoại, bấm một lựa
/// chọn rồi rơi vào node không tồn tại thì không có cách nào thoát ra ngoài việc tải lại trang.
/// Nên mọi quy tắc ở đây đều chặn cứng, không có cảnh báo mềm.
/// </summary>
public class RoleplayValidator
{
    private static readonly string[] ValidQualities = ["good", "curt", "wrong"];

    public IReadOnlyList<ValidationIssue> ValidateOne(RoleplayDocument doc)
    {
        var issues = new List<ValidationIssue>();
        var code = string.IsNullOrWhiteSpace(doc.Code) ? "(chưa có mã)" : doc.Code;

        void Add(string rule, string message) => issues.Add(new ValidationIssue(rule, code, message));

        if (string.IsNullOrWhiteSpace(doc.Code))
        {
            Add("R001", "Thiếu mã kịch bản.");
        }

        if (string.IsNullOrWhiteSpace(doc.TitleVi))
        {
            Add("R002", "Thiếu tiêu đề tiếng Việt.");
        }

        if (string.IsNullOrWhiteSpace(doc.ContextVi))
        {
            Add("R003", "Thiếu bối cảnh. Học viên phải biết mình đang là ai và cần gì trước khi nói.");
        }

        if (string.IsNullOrWhiteSpace(doc.PartnerName))
        {
            Add("R004", "Thiếu tên nhân vật đối thoại.");
        }

        if (doc.Nodes.Count < 5)
        {
            Add("R005", $"Chỉ có {doc.Nodes.Count} lượt. Dưới 5 lượt thì chưa thành hội thoại.");
        }

        if (doc.Nodes.Count > 12)
        {
            Add("R006", $"Có {doc.Nodes.Count} lượt. Trên 12 lượt là quá dài cho một buổi học.");
        }

        var byCode = new Dictionary<string, RoleplayNodeDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in doc.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Code))
            {
                Add("R007", "Có node thiếu mã.");
                continue;
            }

            if (!byCode.TryAdd(node.Code, node))
            {
                Add("R008", $"Mã node {node.Code} bị trùng.");
            }
        }

        foreach (var node in doc.Nodes.Where(n => !string.IsNullOrWhiteSpace(n.Code)))
        {
            ValidateNode(node, byCode, Add);
        }

        if (string.IsNullOrWhiteSpace(doc.StartNode))
        {
            Add("R009", "Thiếu start_node.");
        }
        else if (!byCode.ContainsKey(doc.StartNode))
        {
            Add("R010", $"start_node trỏ tới {doc.StartNode} nhưng không có node nào mang mã đó.");
        }
        else
        {
            ValidateGraph(doc, byCode, Add);
        }

        return issues;
    }

    public IReadOnlyList<ValidationIssue> ValidateSet(IReadOnlyCollection<RoleplayDocument> docs)
    {
        var issues = new List<ValidationIssue>();

        foreach (var doc in docs)
        {
            issues.AddRange(ValidateOne(doc));
        }

        foreach (var group in docs
            .Where(d => !string.IsNullOrWhiteSpace(d.Code))
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            issues.Add(new ValidationIssue("R020", group.Key, $"Mã kịch bản {group.Key} xuất hiện {group.Count()} lần."));
        }

        return issues;
    }

    private static void ValidateNode(
        RoleplayNodeDocument node,
        IReadOnlyDictionary<string, RoleplayNodeDocument> byCode,
        Action<string, string> add)
    {
        if (string.IsNullOrWhiteSpace(node.PartnerLineEn))
        {
            add("R011", $"Node {node.Code} thiếu lời tiếng Anh của nhân vật.");
        }

        if (string.IsNullOrWhiteSpace(node.PartnerLineVi))
        {
            add("R012", $"Node {node.Code} thiếu bản dịch tiếng Việt. Học viên mất gốc cần nó để hiểu tình huống.");
        }

        if (node.Terminal)
        {
            if (node.Choices.Count > 0)
            {
                add("R013", $"Node kết thúc {node.Code} vẫn có lựa chọn.");
            }

            if (string.IsNullOrWhiteSpace(node.SummaryVi))
            {
                add("R014", $"Node kết thúc {node.Code} thiếu tổng kết. Kết thúc mà không nói vì sao là bỏ rơi học viên.");
            }

            return;
        }

        if (node.Choices.Count < 2)
        {
            add("R015", $"Node {node.Code} chỉ có {node.Choices.Count} lựa chọn. Một lựa chọn thì không phải là chọn.");
        }

        if (node.Choices.Count > 4)
        {
            add("R016", $"Node {node.Code} có {node.Choices.Count} lựa chọn. Quá bốn thì học viên đọc mệt hơn là học.");
        }

        foreach (var choice in node.Choices)
        {
            if (string.IsNullOrWhiteSpace(choice.En))
            {
                add("R017", $"Node {node.Code} có lựa chọn thiếu câu tiếng Anh.");
            }

            if (!ValidQualities.Contains(choice.Quality, StringComparer.OrdinalIgnoreCase))
            {
                add("R018", $"Node {node.Code}: quality \"{choice.Quality}\" không hợp lệ. Chỉ nhận good, curt, wrong.");
            }

            // Lựa chọn sai mà không giải thích thì học viên chỉ biết mình sai chứ không biết vì sao.
            if (!string.Equals(choice.Quality, "good", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(choice.FeedbackVi))
            {
                add("R019", $"Node {node.Code}: lựa chọn \"{Trim(choice.En)}\" là {choice.Quality} nhưng thiếu feedback_vi.");
            }

            if (!string.IsNullOrWhiteSpace(choice.Next) && !byCode.ContainsKey(choice.Next))
            {
                add("R021", $"Node {node.Code} có lựa chọn trỏ tới {choice.Next} nhưng node đó không tồn tại.");
            }
        }

        // Mỗi lượt phải có ít nhất một cách trả lời đúng, nếu không đó là bẫy chứ không phải bài học.
        if (!node.Choices.Any(c => string.Equals(c.Quality, "good", StringComparison.OrdinalIgnoreCase)))
        {
            add("R022", $"Node {node.Code} không có lựa chọn nào đạt. Mọi đường đều sai là bẫy, không phải bài học.");
        }
    }

    /// <summary>
    /// Kiểm tính liên thông: mọi node phải tới được từ node bắt đầu, và mọi đường đi
    /// phải kết thúc được.
    /// </summary>
    private static void ValidateGraph(
        RoleplayDocument doc,
        IReadOnlyDictionary<string, RoleplayNodeDocument> byCode,
        Action<string, string> add)
    {
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(doc.StartNode);
        reachable.Add(doc.StartNode);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!byCode.TryGetValue(current, out var node))
            {
                continue;
            }

            foreach (var next in node.Choices
                .Select(c => c.Next)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!))
            {
                if (byCode.ContainsKey(next) && reachable.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        foreach (var orphan in byCode.Keys.Where(c => !reachable.Contains(c)))
        {
            add("R023", $"Node {orphan} không tới được từ node bắt đầu. Nội dung viết ra mà không ai gặp.");
        }

        if (!byCode.Values.Any(n => n.Terminal))
        {
            add("R024", "Kịch bản không có node kết thúc nào. Hội thoại sẽ không bao giờ dừng.");
        }

        if (!byCode.Values.Any(n => n.Terminal && n.Success))
        {
            add("R025", "Không có kết thúc thành công nào. Học viên làm đúng hết vẫn không qua được.");
        }
    }

    private static string Trim(string text) =>
        text.Length <= 40 ? text : text[..40] + "...";
}
