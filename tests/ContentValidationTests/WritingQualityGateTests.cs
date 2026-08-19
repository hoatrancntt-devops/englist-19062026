using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Content;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnglishForIT.ContentValidationTests;

/// <summary>
/// Cổng chất lượng bộ bài luyện viết, chạy trên nội dung thật trong content/writing.
///
/// Bài viết hỏng chấm sai điểm mà vẫn trông như đang chạy đúng — học viên viết đúng bị 0
/// sẽ tin là mình sai. Đó là loại lỗi không ai báo, nên phải chặn ở đây.
/// </summary>
public class WritingQualityGateTests
{
    private static readonly WritingLoadResult Loaded = LoadSets();
    private static readonly WritingValidator Validator = new();

    [Fact]
    public void MoiFileBoBaiDeuDocDuoc()
    {
        Assert.Empty(Loaded.Errors.Select(e => $"{Path.GetFileName(e.FilePath)}: {e.Message}"));
    }

    [Fact]
    public void CoItNhatMotBoBai()
    {
        Assert.NotEmpty(Loaded.Sets);
    }

    [Fact]
    public void ToanBoBoBaiQuaCongChatLuong()
    {
        var issues = Validator.ValidateSet([.. Loaded.Sets.Select(s => s.Document)]);

        Assert.Empty(issues.Select(i => i.ToString()));
    }

    [Fact]
    public void SoChoTrongKhopSoDauGachTrongCau()
    {
        // Số ô nhập hiện ra lấy từ blanks.Count, còn học viên nhìn vào dấu ___ trong câu.
        // Lệch nhau thì có ô thừa không biết điền gì, hoặc có chỗ trống không có ô.
        foreach (var set in Loaded.Sets.Select(s => s.Document))
        {
            foreach (var task in set.Tasks.Where(t => t.Kind == WritingTaskKind.FillBlank))
            {
                var markers = CountMarkers(task.PromptEn);

                Assert.True(markers == task.Blanks.Count,
                    $"{set.Code}/{task.Code}: câu có {markers} dấu ___ nhưng khai {task.Blanks.Count} chỗ trống.");
            }
        }
    }

    [Fact]
    public void CauMauKhongDeLaiDauGach()
    {
        // Câu mẫu là thứ học viên đọc để biết đúng trông thế nào. Còn ___ trong đó là chưa điền xong.
        foreach (var set in Loaded.Sets.Select(s => s.Document))
        {
            foreach (var task in set.Tasks)
            {
                Assert.False(task.SampleEn.Contains("___", StringComparison.Ordinal),
                    $"{set.Code}/{task.Code}: câu mẫu vẫn còn dấu ___.");
            }
        }
    }

    [Fact]
    public void MoiBoDeuCoDuBaDangBai()
    {
        // Một bộ toàn điền chỗ trống thì luyện được đúng một kỹ năng. Ba dạng đo ba thứ khác nhau.
        foreach (var set in Loaded.Sets.Select(s => s.Document))
        {
            var kinds = set.Tasks.Select(t => t.Kind).Distinct().Count();

            Assert.True(kinds >= 2, $"{set.Code}: chỉ có {kinds} dạng bài. Cần ít nhất 2 dạng.");
        }
    }

    [Fact]
    public void HashOnDinhGiuaHaiLanDoc()
    {
        var again = LoadSets();

        foreach (var set in Loaded.Sets)
        {
            var match = again.Sets.Single(s => s.Document.Code == set.Document.Code);
            Assert.Equal(set.SourceHash, match.SourceHash);
        }
    }

    private static int CountMarkers(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var count = 0;
        var index = text.IndexOf("___", StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;

            // Nhảy qua cả cụm gạch để "____" dài hơn ba gạch vẫn tính là một chỗ trống.
            while (index < text.Length && text[index] == '_')
            {
                index++;
            }

            index = text.IndexOf("___", index, StringComparison.Ordinal);
        }

        return count;
    }

    private static WritingLoadResult LoadSets()
    {
        var loader = new YamlContentLoader(NullLogger<YamlContentLoader>.Instance);
        return loader.LoadWritingSets(FindContentRoot());
    }

    /// <summary>Đi ngược lên từ thư mục chạy test tới khi thấy thư mục content/.</summary>
    private static string FindContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "content");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục content/ từ chỗ chạy test.");
    }
}
