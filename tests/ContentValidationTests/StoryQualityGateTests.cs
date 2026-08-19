using EnglishForIT.Application.Content;
using EnglishForIT.Infrastructure.Content;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnglishForIT.ContentValidationTests;

/// <summary>
/// Cổng chất lượng chương truyện, chạy trên nội dung thật trong content/story.
///
/// Truyện không chấm điểm nên nó hỏng lặng lẽ: một mốc mở sai chính tả làm chương khoá
/// vĩnh viễn, và không có học viên nào báo lỗi vì họ không biết là đáng lẽ phải có chương.
/// Đó là lý do mốc mở được đối chiếu với danh sách bài thật ở đây.
/// </summary>
public class StoryQualityGateTests
{
    private static readonly StoryLoadResult Loaded = LoadStories();
    private static readonly LoadResult Lessons = LoadLessons();
    private static readonly StoryValidator Validator = new();

    [Fact]
    public void MoiFileChuongDeuDocDuoc()
    {
        Assert.Empty(Loaded.Errors.Select(e => $"{Path.GetFileName(e.FilePath)}: {e.Message}"));
    }

    [Fact]
    public void CoItNhatMotChuong()
    {
        // Cùng cái bẫy với bài học: thư mục rỗng mà test xanh thì mọi kiểm tra bên dưới vô nghĩa.
        Assert.NotEmpty(Loaded.Chapters);
    }

    [Fact]
    public void ToanBoChuongQuaCongChatLuong()
    {
        var lessonCodes = Lessons.Lessons
            .Select(l => l.Document.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var issues = Validator.ValidateSet([.. Loaded.Chapters.Select(c => c.Document)], lessonCodes);

        Assert.Empty(issues.Select(i => i.ToString()));
    }

    [Fact]
    public void MoiMocMoTroToiMotBaiCoThat()
    {
        var lessonCodes = Lessons.Lessons
            .Select(l => l.Document.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var chapter in Loaded.Chapters.Select(c => c.Document))
        {
            Assert.True(lessonCodes.Contains(chapter.UnlockAfterLesson),
                $"{chapter.Code} mở tại {chapter.UnlockAfterLesson} nhưng không có bài nào mang mã đó.");
        }
    }

    [Fact]
    public void KhongCoHaiChuongCungMocMo()
    {
        // Hai chương cùng mốc thì bật ra một lúc rồi im bặt hàng chục bài — mạch truyện
        // mất đúng tác dụng giữ chân mà nó sinh ra để làm.
        var duplicates = Loaded.Chapters
            .GroupBy(c => c.Document.UnlockAfterLesson, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void SoChuongLienMachTuMot()
    {
        var numbers = Loaded.Chapters.Select(c => c.Document.Number).OrderBy(n => n).ToList();

        Assert.Equal(Enumerable.Range(1, numbers.Count), numbers);
    }

    [Fact]
    public void HashOnDinhGiuaHaiLanDoc()
    {
        var again = LoadStories();

        foreach (var chapter in Loaded.Chapters)
        {
            var match = again.Chapters.Single(c => c.Document.Code == chapter.Document.Code);
            Assert.Equal(chapter.SourceHash, match.SourceHash);
        }
    }

    private static StoryLoadResult LoadStories()
    {
        var loader = new YamlContentLoader(NullLogger<YamlContentLoader>.Instance);
        return loader.LoadStoryChapters(FindContentRoot());
    }

    private static LoadResult LoadLessons()
    {
        var loader = new YamlContentLoader(NullLogger<YamlContentLoader>.Instance);
        return loader.LoadLessons(FindContentRoot());
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
