using System.Text.RegularExpressions;
using EnglishForIT.Application.Content;

namespace EnglishForIT.ContentValidationTests;

/// <summary>
/// Bắt cầu giữa danh mục hình phía máy chủ và bảng tra phía giao diện.
///
/// E110 chặn được khoá lạ trong YAML, nhưng không ai chặn trường hợp khoá có thật
/// trong danh mục mà giao diện lại thiếu component tương ứng. Lúc đó bài rơi về
/// hình bàn làm việc, không có lỗi, không có cảnh báo — chỉ có một bài học sai hình
/// mà phải mở đúng bài đó mới thấy.
///
/// Test này đọc thẳng tệp TSX thay vì chạy Node, vì mục đích chỉ là so hai danh sách
/// khoá. Kéo cả bộ công cụ JavaScript vào chỉ để làm việc đó là quá đắt.
/// </summary>
public class IllustrationCoverageTests
{
    private static readonly string[] SceneFiles =
    [
        Path.Combine("apps", "web", "src", "components", "illustrations", "scene-illustrations.tsx"),
        Path.Combine("apps", "web", "src", "components", "illustrations", "tech-scene-illustrations.tsx"),
    ];

    [Fact]
    public void MoiKhoaTrongDanhMucDeuCoComponentOGiaoDien()
    {
        var rendered = ReadRenderedKeys();

        var missing = IllustrationCatalogue.All.Where(key => !rendered.Contains(key)).Order();

        Assert.Empty(missing.Select(key =>
            $"Khoá \"{key}\" có trong IllustrationCatalogue nhưng không có component nào ở giao diện. " +
            "Bài dùng khoá này sẽ hiện hình mặc định mà không báo lỗi."));
    }

    [Fact]
    public void GiaoDienKhongCoCanhThua()
    {
        var rendered = ReadRenderedKeys();

        var orphaned = rendered.Where(key => !IllustrationCatalogue.IsKnown(key)).Order();

        // Cảnh thừa không làm hỏng gì ngay, nhưng nó là dấu hiệu ai đó thêm hình
        // rồi quên khai vào danh mục — và bài học sẽ không bao giờ dùng được nó.
        Assert.Empty(orphaned.Select(key =>
            $"Giao diện có cảnh \"{key}\" nhưng IllustrationCatalogue không khai. " +
            "Không bài nào dùng được nó vì E110 sẽ chặn."));
    }

    private static HashSet<string> ReadRenderedKeys()
    {
        var root = FindRepoRoot();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var relative in SceneFiles)
        {
            var path = Path.Combine(root, relative);
            Assert.True(File.Exists(path), $"Không thấy tệp cảnh: {relative}");

            // Chỉ lấy các dòng dạng  'ten-canh': Component,  trong bảng tra.
            foreach (Match match in Regex.Matches(File.ReadAllText(path), @"'([a-z0-9-]+)':\s*[A-Z]\w*,"))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "apps", "web")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy gốc kho từ thư mục chạy test.");
    }
}
