using System.Security.Claims;
using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.Api.Modules;

/// <summary>Một bài trong đồ thị, kèm các số chỉ nhìn cả đồ thị mới tính được.</summary>
public record GraphNode(
    string Code,
    string TitleVi,
    string Track,
    string Level,
    string Layer,
    string Status,
    string? UnitCode,
    int OrderIndex,
    bool IsCheckpoint,
    int Activities,
    int Items,
    /// <summary>Số bài phải học xong trước khi tới được bài này, tính theo đường dài nhất.</summary>
    int Depth,
    /// <summary>Số bài bị bài này chặn, tính cả gián tiếp. Cao nghĩa là nút thắt.</summary>
    int Gates);

public record GraphEdge(string From, string To, string Kind, int MinMastery);

/// <summary>Vấn đề về hình dạng đồ thị. Khác với cổng validate lúc seed: cổng đó xét từng file YAML.</summary>
public record GraphProblem(string Code, string Severity, string LessonCode, string Message);

public record ContentGraph(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyList<GraphProblem> Problems,
    int MaxDepth);

/// <summary>
/// Tra cứu nội dung và soi đồ thị tiên quyết.
///
/// <b>Chỉ đọc, cố ý.</b> Nguồn sự thật của nội dung là file YAML; seeder upsert theo mã bài và
/// bỏ qua bài còn nguyên hash. Cho sửa bài thẳng vào DB thì lần nạp lại kế tiếp ghi đè mất mà
/// không báo gì — nên biên tập vẫn đi qua file, còn màn này lo phần file không cho thấy được:
/// hình dạng của cả đồ thị khi 58 bài ghép lại.
///
/// Cổng validate lúc seed đã chặn cạnh treo, đảo bậc và chu trình. Những gì tính ở đây là thứ
/// cổng đó không thấy vì nó xét từng file: chuỗi tiên quyết dài bao nhiêu so với cả kho bài,
/// và cạnh nào trỏ tới bài chưa xuất bản.
/// </summary>
public static class AdminContentModule
{
    /// <summary>Đường dài nhất phủ bao nhiêu phần đồ thị thì coi là lộ trình tuyến tính.</summary>
    private const double ChainShare = 0.5;

    public static IEndpointRouteBuilder MapAdminContentModule(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/content/graph", Graph)
            .WithTags("Admin")
            .WithSummary("Đồ thị tiên quyết kèm độ sâu, số bài bị chặn và các vấn đề về hình dạng");

        return app;
    }

    private static async Task<IResult> Graph(
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!AdminAccess.IsAdmin(principal))
        {
            return AdminAccess.Denied();
        }

        // Lấy cả bài nháp: chính cặp published-phụ-thuộc-nháp mới là thứ cần soi,
        // lọc mất bài nháp thì không còn thấy nó nữa.
        var lessons = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Prerequisites)
            .OrderBy(l => l.Track).ThenBy(l => l.OrderIndex)
            .ToListAsync(ct);

        if (lessons.Count == 0)
        {
            return Results.Ok(new ContentGraph([], [], [], 0));
        }

        var counts = await db.LessonActivities
            .AsNoTracking()
            .GroupBy(a => a.LessonId)
            .Select(g => new { LessonId = g.Key, Activities = g.Count(), Items = g.Sum(a => a.Items.Count) })
            .ToDictionaryAsync(x => x.LessonId, ct);

        var codeById = lessons.ToDictionary(l => l.Id, l => l.Code);
        var byCode = lessons.ToDictionary(l => l.Code, StringComparer.Ordinal);

        var edges = lessons
            .SelectMany(l => l.Prerequisites
                .Where(p => codeById.ContainsKey(p.RequiredLessonId))
                .Select(p => new GraphEdge(codeById[p.RequiredLessonId], l.Code, p.Kind.ToString(), p.MinMastery)))
            .OrderBy(e => e.From).ThenBy(e => e.To)
            .ToList();

        // Chỉ cạnh Hard mới khoá được bài, nên độ sâu và nút thắt tính trên cạnh Hard.
        // Gộp cả Soft vào thì mọi con số đều thổi phồng so với thứ học viên thật sự gặp.
        var hard = edges.Where(e => e.Kind == nameof(PrerequisiteKind.Hard)).ToList();

        var shape = PrerequisiteGraphMath.Measure(
            byCode.Keys,
            hard.Select(e => new GraphLink(e.From, e.To)).ToList());

        var nodes = lessons
            .Select(l => new GraphNode(
                l.Code,
                l.TitleVi,
                l.Track.ToString(),
                l.Level.ToString(),
                l.Layer.ToString(),
                l.Status.ToString(),
                l.UnitCode,
                l.OrderIndex,
                l.IsCheckpoint,
                counts.GetValueOrDefault(l.Id)?.Activities ?? 0,
                counts.GetValueOrDefault(l.Id)?.Items ?? 0,
                shape.Depth.GetValueOrDefault(l.Code, 0),
                shape.Gates.GetValueOrDefault(l.Code, 0)))
            .ToList();

        var maxDepth = shape.Depth.Count == 0 ? 0 : shape.Depth.Values.Max();

        return Results.Ok(new ContentGraph(
            nodes,
            edges,
            FindProblems(byCode, edges, shape, maxDepth),
            maxDepth));
    }


    private static List<GraphProblem> FindProblems(
        Dictionary<string, Domain.Entities.Content.Lesson> byCode,
        IReadOnlyList<GraphEdge> edges,
        GraphShape shape,
        int maxDepth)
    {
        var problems = new List<GraphProblem>();

        // G01 — cạnh trỏ tới bài chưa xuất bản.
        //
        // Lộ trình chỉ nạp bài Published rồi BỎ QUA cạnh trỏ ra ngoài tập đó. Nên bài tiên quyết
        // còn nháp không làm học viên kẹt — nó làm cái cổng BIẾN MẤT, và bài phía sau mở ra cho
        // người chưa học phần nền. Hỏng theo chiều dễ bỏ sót hơn hẳn chiều kẹt.
        foreach (var edge in edges)
        {
            if (byCode.TryGetValue(edge.From, out var required)
                && required.Status != ContentStatus.Published
                && byCode.TryGetValue(edge.To, out var dependent)
                && dependent.Status == ContentStatus.Published)
            {
                problems.Add(new GraphProblem("G01", "error", edge.To,
                    $"Tiên quyết {edge.From} đang ở trạng thái {required.Status}. Lộ trình bỏ qua cạnh này, "
                    + "nên bài mở ra cho cả người chưa học phần nền."));
            }
        }

        // G02 — đồ thị gần như một đường thẳng.
        //
        // Cố ý báo Ở MỨC CẢ ĐỒ THỊ, không báo từng bài. Bản đầu tôi gắn cờ mọi bài chặn quá
        // một phần tư số bài còn lại; trên nội dung thật nó bắn 34 cảnh báo trên 58 bài. Đúng
        // về số học nhưng vô dụng: trong một chuỗi thẳng thì đương nhiên bài nào cũng chặn hết
        // phần đuôi, nên xếp hạng từng nút chỉ đang xếp hạng vị trí trong hàng.
        //
        // Đây cũng KHÔNG phải lỗi. Lộ trình thẳng hợp với người mất gốc: không có ngã rẽ nào
        // để đi sai. Cái giá là người chỉ cần tiếng Anh mảng Cloud vẫn phải cày hết phần đời
        // sống trước. Đánh đổi đó thuộc về người làm nội dung quyết, nên chỉ báo, không cảnh cáo.
        var longestShare = byCode.Count == 0 ? 0 : (double)(maxDepth + 1) / byCode.Count;

        if (longestShare >= ChainShare)
        {
            problems.Add(new GraphProblem("G02", "info", "",
                $"Đường dài nhất đi qua {maxDepth + 1}/{byCode.Count} bài "
                + $"({longestShare:P0}), nên lộ trình gần như tuyến tính. "
                + "Học viên hầu như không có lựa chọn thứ tự: muốn tới bài cuối phải qua gần hết bài trước."));
        }

        // G03 — chu trình.
        foreach (var code in shape.InCycle.OrderBy(c => c))
        {
            problems.Add(new GraphProblem("G03", "error", code,
                "Nằm trong chu trình tiên quyết nên không bao giờ mở được. DB đã bị sửa ngoài đường seeder."));
        }

        return problems;
    }
}
