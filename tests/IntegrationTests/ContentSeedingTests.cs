using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishForIT.IntegrationTests;

/// <summary>
/// Seeder chạy trên Postgres thật.
///
/// Đây là chỗ test đơn vị không với tới được: seeder từng làm sập API hai lần vì cách EF
/// sinh lệnh UPDATE và DELETE, mà cả hai lần đều không lỗi trên DB trong bộ nhớ.
/// </summary>
[Collection(ApiCollection.Name)]
public class ContentSeedingTests(ApiFactory api)
{
    [Fact]
    public async Task NapDuNoiDungThatVaoDb()
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        var lessons = await db.Lessons.CountAsync(l => l.Status == ContentStatus.Published);
        var activities = await db.LessonActivities.CountAsync();
        var items = await db.LessonItems.CountAsync();
        var forms = await db.PlacementForms.CountAsync();

        Assert.Equal(70, lessons);
        Assert.Equal(490, activities);
        Assert.Equal(666, items);
        Assert.Equal(2, forms);
    }

    [Fact]
    public async Task ChayLaiSeederKhongDoiGiVaKhongDungVaoTienDo()
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);
        var seeder = scope.ServiceProvider.GetRequiredService<ContentSeeder>();

        var itemIdsBefore = await db.LessonItems.Select(i => i.Id).OrderBy(id => id).ToListAsync();

        var report = await seeder.SeedAsync(ContentRoot());

        Assert.Empty(report.Problems);
        Assert.Equal(0, report.Inserted);
        Assert.Equal(0, report.Updated);
        Assert.Equal(70, report.Unchanged);

        // Id giữ nguyên là bằng chứng seeder upsert chứ không xoá rồi tạo lại — nếu tạo lại,
        // khoá ngoại từ hàng đợi ôn tập của học viên sẽ bị cascade xoá theo.
        var itemIdsAfter = await db.LessonItems.Select(i => i.Id).OrderBy(id => id).ToListAsync();
        Assert.Equal(itemIdsBefore, itemIdsAfter);
    }

    [Fact]
    public async Task ChayLaiSeederDeXepLopKhongDoiGi()
    {
        await using var scope = api.NewScope();
        var seeder = scope.ServiceProvider.GetRequiredService<PlacementSeeder>();

        var report = await seeder.SeedAsync(ContentRoot());

        Assert.Empty(report.Problems);
        Assert.Equal(0, report.Inserted);
        Assert.Equal(0, report.Updated);
        Assert.Equal(2, report.Unchanged);
    }

    [Fact]
    public async Task MoiBaiDeuCoKhoaHinhMinhHoa()
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        // Khoá rỗng làm bài rơi về hình mặc định mà không báo lỗi nào.
        //
        // KHÔNG đòi mỗi bài một khoá riêng: 67 bài dùng chung khoảng 33 khoá là có chủ đích —
        // shield-lock cho ba bài bảo mật, clock-calendar cho các bài về thời gian.
        // Hình minh hoạ theo chủ đề chứ không theo bài.
        var illustrations = await db.Lessons
            .Where(l => l.Status == ContentStatus.Published)
            .Select(l => l.Illustration)
            .ToListAsync();

        Assert.Equal(70, illustrations.Count);
        Assert.DoesNotContain(illustrations, i => string.IsNullOrWhiteSpace(i));
    }

    [Fact]
    public async Task DapAnKhongDonVeMotViTriTrenToanGiaoTrinh()
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        var answers = await db.LessonItems.Select(i => i.AnswerJson).ToListAsync();

        var positions = answers
            .Select(a => System.Text.Json.JsonDocument.Parse(a).RootElement)
            .Where(root => root.TryGetProperty("Answer", out _))
            .Select(root => root.GetProperty("Answer").GetInt32())
            .ToList();

        var mostCommon = positions.GroupBy(p => p).OrderByDescending(g => g.Count()).First();

        // Từng có lúc 79% câu dồn về vị trí 1: chọn mãi ô đó là qua gần hết giáo trình,
        // và qua luôn bài thi vượt vốn bỏ qua mọi tiên quyết.
        Assert.True(
            mostCommon.Count() * 2 <= positions.Count,
            $"{mostCommon.Count()} trên {positions.Count} câu có đáp án ở vị trí {mostCommon.Key}");
    }

    [Fact]
    public async Task DoiTienQuyetTrongYamlRoiSeedLaiThiKhongSap()
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);
        var seeder = scope.ServiceProvider.GetRequiredService<ContentSeeder>();

        // Nội dung sửa trên bản sao, không đụng file thật trong repo.
        var edited = CopyContentToTempAsync();

        try
        {
            var file = Directory
                .EnumerateFiles(Path.Combine(edited, "lessons"), "CLD-03.yaml", SearchOption.AllDirectories)
                .Single();

            // Chuẩn hoá xuống dòng trước khi so khớp: file trong repo dùng CRLF.
            var before = (await File.ReadAllTextAsync(file)).Replace("\r\n", "\n");

            // THÊM một cạnh chưa từng có. Chỉ đường "thêm cạnh mới" mới làm vỡ seeder — đổi loại
            // hay đổi ngưỡng của cạnh đã có thì không sao.
            //
            // Chèn vào đầu danh sách chứ không thay một cạnh cụ thể, để test không phụ thuộc vào
            // nội dung thật của bài. Bản trước gắn cứng cặp CLD-01/INF-05, rồi chính giáo trình
            // đổi đúng cặp đó và test lặng lẽ thành vô nghĩa.
            Assert.DoesNotContain("lesson: OFF-01", before);

            var after = before.Replace(
                "prerequisites:\n",
                "prerequisites:\n  - lesson: OFF-01\n    min_mastery: 65\n    kind: Soft\n");

            Assert.NotEqual(before, after);
            await File.WriteAllTextAsync(file, after);

            // Đây là chỗ seeder từng ném DbUpdateConcurrencyException và làm API chết lúc khởi
            // động. Đường "nội dung ĐÃ ĐỔI rồi seed lại" trước đây không test nào đi qua:
            // mọi test chỉ chạy lại seeder với nội dung y nguyên.
            var report = await seeder.SeedAsync(edited);

            Assert.Empty(report.Problems);
            Assert.Equal(1, report.Updated);

            // Cạnh MỚI phải vào được DB, và phải là ĐÚNG cạnh vừa thêm ở trên.
            //
            // Bản trước kiểm cặp CLD-01/OFF-12 trong khi phần sửa lại nhắm vào CLD-03/OFF-01.
            // Cặp đó vốn đã có sẵn trong giáo trình nên test xanh mà không hề chạm tới thứ nó
            // khai là đang kiểm — hỏng seeder vẫn xanh.
            var added = await db.LessonPrerequisites
                .CountAsync(p => p.Lesson!.Code == "CLD-03" && p.RequiredLesson!.Code == "OFF-01");

            Assert.Equal(1, added);

            var kind = await db.LessonPrerequisites
                .Where(p => p.Lesson!.Code == "CLD-03" && p.RequiredLesson!.Code == "OFF-01")
                .Select(p => p.Kind)
                .SingleAsync();

            Assert.Equal(PrerequisiteKind.Soft, kind);
        }
        finally
        {
            // Trả DB về đúng nội dung thật: các test khác trong tập này dùng chung một container.
            //
            // Dùng scope MỚI, và nuốt lỗi ở đây. DbContext của scope trên có thể đã hỏng sau một
            // lần lưu thất bại, và nếu lần dọn này ném thì nó thay mất ngoại lệ gốc — đúng chuyện
            // vừa xảy ra, làm chẩn đoán biến mất.
            try
            {
                await using var cleanup = api.NewScope();
                await cleanup.ServiceProvider.GetRequiredService<ContentSeeder>().SeedAsync(ContentRoot());
            }
            catch (DbUpdateException)
            {
                // Đã có ngoại lệ gốc để đọc rồi.
            }

            Directory.Delete(edited, recursive: true);
        }
    }

    /// <summary>Bản sao thư mục content vào chỗ tạm, để test sửa file mà không bẩn repo.</summary>
    private static string CopyContentToTempAsync()
    {
        var source = ContentRoot();
        var target = Path.Combine(Path.GetTempPath(), "efit-content-" + Guid.NewGuid().ToString("N"));

        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(source, target));
        }

        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = file.Replace(source, target);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }

        return target;
    }

    private static string ContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "content");

            if (Directory.Exists(Path.Combine(candidate, "lessons")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục content.");
    }
}
