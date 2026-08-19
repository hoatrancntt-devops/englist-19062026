namespace EnglishForIT.Application.Content;

/// <summary>Một cạnh tiên quyết đã rút gọn: chỉ còn hai đầu, đủ để tính hình dạng đồ thị.</summary>
public record GraphLink(string From, string To);

/// <summary>
/// Kết quả đo đồ thị. <paramref name="InCycle"/> rỗng là đồ thị lành.
/// </summary>
public record GraphShape(
    IReadOnlyDictionary<string, int> Depth,
    IReadOnlyDictionary<string, int> Gates,
    IReadOnlySet<string> InCycle);

/// <summary>
/// Phần toán của đồ thị tiên quyết, tách khỏi tầng HTTP để kiểm được trực tiếp.
///
/// Tách ra vì đây là chỗ dễ sai nhất trong cả tính năng: đường dài nhất trên đồ thị có hướng
/// trông rất giống đường ngắn nhất cho tới khi gặp đồ thị chia nhánh rồi nhập lại, và lúc đó
/// sai số không hiện ra ở đâu — chỉ là một con số hơi nhỏ trên màn quản trị.
/// </summary>
public static class PrerequisiteGraphMath
{
    public static GraphShape Measure(IEnumerable<string> nodes, IReadOnlyList<GraphLink> links)
    {
        var codes = nodes.ToList();
        var outgoing = links
            .GroupBy(e => e.From)
            .ToDictionary(g => g.Key, g => g.Select(e => e.To).Distinct().ToList());

        return new GraphShape(
            ComputeDepth(codes, links, outgoing, out var inCycle),
            ComputeGates(codes, outgoing),
            inCycle);
    }

    /// <summary>
    /// Độ sâu = đường DÀI nhất từ một nút không có tiên quyết, tính bằng sắp xếp tô-pô kiểu Kahn.
    ///
    /// Phải là đường dài nhất chứ không phải ngắn nhất: cạnh cứng buộc phải qua hết, nên số bài
    /// thật sự phải học trước là nhánh dài nhất, không phải nhánh nhanh nhất. Đây cũng là lý do
    /// không dùng BFS thường — BFS trả về đường ngắn nhất và sẽ báo thiếu ở mọi chỗ đồ thị
    /// chia nhánh rồi nhập lại.
    /// </summary>
    private static Dictionary<string, int> ComputeDepth(
        List<string> codes,
        IReadOnlyList<GraphLink> links,
        Dictionary<string, List<string>> outgoing,
        out HashSet<string> inCycle)
    {
        var pending = codes.ToDictionary(c => c, _ => 0);

        foreach (var link in links.Where(l => pending.ContainsKey(l.To)))
        {
            pending[link.To]++;
        }

        var depth = codes.ToDictionary(c => c, _ => 0);
        var queue = new Queue<string>(pending.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var settled = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            settled++;

            foreach (var next in outgoing.GetValueOrDefault(current, []))
            {
                depth[next] = Math.Max(depth[next], depth[current] + 1);

                if (--pending[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        // Nút không bao giờ ra khỏi hàng đợi thì nằm trong chu trình: bậc vào của nó không
        // bao giờ về 0 vì có nút phía trước đang chờ chính nó.
        inCycle = settled == codes.Count
            ? []
            : pending.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToHashSet();

        return depth;
    }

    /// <summary>Số nút mà mỗi nút chặn, tính cả gián tiếp.</summary>
    private static Dictionary<string, int> ComputeGates(
        List<string> codes, Dictionary<string, List<string>> outgoing)
    {
        var gates = new Dictionary<string, int>();

        foreach (var code in codes)
        {
            var seen = new HashSet<string>();
            var stack = new Stack<string>([code]);

            while (stack.Count > 0)
            {
                foreach (var next in outgoing.GetValueOrDefault(stack.Pop(), []))
                {
                    if (seen.Add(next))
                    {
                        stack.Push(next);
                    }
                }
            }

            // Bỏ chính nó ra nếu nó nằm trong chu trình và tự quay về được.
            seen.Remove(code);
            gates[code] = seen.Count;
        }

        return gates;
    }
}
