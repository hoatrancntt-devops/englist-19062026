using System.Net.Http.Json;
using System.Text.Json;

using EnglishForIT.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.IntegrationTests;

/// <summary>
/// Đồ thị nội dung và cái cổng biến mất.
///
/// Bất biến chính ở đây: hạ một bài tiên quyết xuống nháp phải KHOÁ bài phía sau, không được
/// mở nó ra. Bản đầu làm ngược lại — lộ trình lọc theo tập đã xuất bản rồi lặng lẽ bỏ mọi cạnh
/// trỏ ra ngoài tập đó, nên một bài ở bậc 18 thành điểm vào ngày một. Không có test thì lần
/// sửa sau rất dễ khôi phục đúng cái phép lọc đó.
/// </summary>
[Collection(ApiCollection.Name)]
public class ContentGraphTests(ApiFactory api)
{
    [Fact]
    public async Task DoThiKhopVoiNoiDungThat()
    {
        var admin = await api.NewAdminAsync();

        using var graph = JsonDocument.Parse(
            await (await admin.GetAsync("/api/v1/admin/content/graph")).Content.ReadAsStringAsync());

        var nodes = graph.RootElement.GetProperty("nodes");
        var maxDepth = graph.RootElement.GetProperty("maxDepth").GetInt32();

        Assert.True(nodes.GetArrayLength() > 0);

        // Bậc phải nằm trong khoảng đã công bố, nếu không thì cột Bậc trên màn quản trị vô nghĩa.
        Assert.All(
            nodes.EnumerateArray(),
            node =>
            {
                var depth = node.GetProperty("depth").GetInt32();
                Assert.InRange(depth, 0, maxDepth);
            });

        // Phải có ít nhất một bài bậc 0, nếu không thì không ai vào học được.
        Assert.Contains(nodes.EnumerateArray(), n => n.GetProperty("depth").GetInt32() == 0);
    }

    [Fact]
    public async Task NoiDungThatKhongCoLoiVeHinhDangDoThi()
    {
        var admin = await api.NewAdminAsync();

        using var graph = JsonDocument.Parse(
            await (await admin.GetAsync("/api/v1/admin/content/graph")).Content.ReadAsStringAsync());

        // Mức info là nhận xét về lộ trình, không phải lỗi. Chỉ error mới là thứ phải sửa.
        var errors = graph.RootElement.GetProperty("problems")
            .EnumerateArray()
            .Where(p => p.GetProperty("severity").GetString() == "error")
            .Select(p => p.GetProperty("message").GetString())
            .ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public async Task TienQuyetBiHaXuongNhapThiKhoaBaiChuKhongMoBai()
    {
        var admin = await api.NewAdminAsync();

        var (dependent, required) = await FindGatedPairAsync();

        await SetStatusAsync(required, ContentStatus.Draft);

        try
        {
            // Khu quản trị phải chỉ đúng tên bài gây ra chuyện đó.
            using var graph = JsonDocument.Parse(
                await (await admin.GetAsync("/api/v1/admin/content/graph")).Content.ReadAsStringAsync());

            var flagged = graph.RootElement.GetProperty("problems")
                .EnumerateArray()
                .Where(p => p.GetProperty("code").GetString() == "G01")
                .Select(p => p.GetProperty("message").GetString() ?? "")
                .ToList();

            Assert.Contains(flagged, message => message.Contains(required));

            // Và cổng vẫn phải còn. Trước khi sửa, chính ở đây bài nhảy sang Available.
            var fresh = await api.NewLearnerAsync();
            var state = await StateOfAsync(fresh, dependent);

            Assert.True(state == "Locked", $"{dependent} (cổng duy nhất {required}) ở trạng thái {state}.");
        }
        finally
        {
            await SetStatusAsync(required, ContentStatus.Published);
        }
    }

    /// <summary>
    /// Một bài đang khoá với học viên mới, có ĐÚNG MỘT tiên quyết cứng, và tiên quyết đó
    /// cũng đang xuất bản.
    ///
    /// Cả hai điều kiện đều cần thiết, và bản đầu của test này thiếu cả hai nên xanh luôn với
    /// mã hỏng. Phải đúng một tiên quyết cứng: còn cái thứ hai thì bài vẫn khoá dù cổng đang xét
    /// đã rơi mất. Phải đang khoá sẵn: bài vốn đã mở thì không chứng minh được điều gì khi nó
    /// vẫn mở sau lúc hạ nháp.
    ///
    /// Chọn theo dữ liệu thật thay vì gán cứng một mã bài, để nội dung đổi thì test tự tìm cặp khác.
    /// </summary>
    private async Task<(string Dependent, string Required)> FindGatedPairAsync()
    {
        List<(string Dependent, string Required)> candidates;

        await using (var scope = api.NewScope())
        {
            var rows = await ApiFactory.Db(scope).Lessons
                .AsNoTracking()
                .Where(l => l.Status == ContentStatus.Published)
                .Where(l => l.Prerequisites.Count(p => p.Kind == PrerequisiteKind.Hard) == 1)
                .Select(l => new
                {
                    Dependent = l.Code,
                    Required = l.Prerequisites
                        .Where(p => p.Kind == PrerequisiteKind.Hard
                            && p.RequiredLesson!.Status == ContentStatus.Published)
                        .Select(p => p.RequiredLesson!.Code)
                        .ToList(),
                })
                .ToListAsync();

            candidates = rows
                .Where(r => r.Required.Count == 1)
                .Select(r => (r.Dependent, r.Required[0]))
                .ToList();
        }

        var learner = await api.NewLearnerAsync();

        using var roadmap = JsonDocument.Parse(
            await (await learner.GetAsync("/api/v1/learning/roadmap")).Content.ReadAsStringAsync());

        var locked = roadmap.RootElement.GetProperty("lessons")
            .EnumerateArray()
            .Where(l => l.GetProperty("state").GetString() == "Locked")
            .Select(l => l.GetProperty("code").GetString())
            .ToHashSet();

        var pair = candidates.FirstOrDefault(c => locked.Contains(c.Dependent));

        Assert.True(
            pair.Dependent is not null,
            "Nội dung không còn bài nào vừa khoá vừa chỉ có một tiên quyết cứng, "
            + "nên không dựng được tình huống cần kiểm. Sửa phép chọn chứ đừng bỏ test.");

        return pair;
    }

    private async Task<string?> StateOfAsync(HttpClient learner, string code)
    {
        using var roadmap = JsonDocument.Parse(
            await (await learner.GetAsync("/api/v1/learning/roadmap")).Content.ReadAsStringAsync());

        return roadmap.RootElement.GetProperty("lessons")
            .EnumerateArray()
            .FirstOrDefault(l => l.GetProperty("code").GetString() == code)
            .GetProperty("state").GetString();
    }

    private async Task SetStatusAsync(string code, ContentStatus status)
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        var lesson = await db.Lessons.FirstAsync(l => l.Code == code);
        lesson.Status = status;

        await db.SaveChangesAsync();
    }
}
