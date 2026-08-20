using EnglishForIT.Application.Content;
using EnglishForIT.Infrastructure.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace EnglishForIT.ContentValidationTests;

/// <summary>
/// Cổng chất lượng cho bộ từ vựng tần suất cao.
///
/// Khác cổng của bài học ở chỗ nó chặn CỨNG: nội dung này mới hoàn toàn nên không có mục nào
/// được nợ emoji hay mẹo nhớ, và không từ nào được trùng với thứ bài học đã dạy.
/// </summary>
public class VocabDeckQualityGateTests(ITestOutputHelper output)
{
    private static readonly YamlContentLoader Loader = new(NullLogger<YamlContentLoader>.Instance);

    [Fact]
    public void MoiBoPhaiQuaCongKiemDinh()
    {
        var load = Loader.LoadVocabDecks(FindContentRoot());

        Assert.Empty(load.Errors.Select(e => $"{Path.GetFileName(e.FilePath)}: {e.Message}"));

        var issues = load.Decks
            .SelectMany(d => VocabDeckValidator.Validate(d.Document))
            .Select(i => i.ToString())
            .ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void KhongTuNaoTrungVoiTuDaDayTrongBaiHoc()
    {
        var root = FindContentRoot();
        var decks = Loader.LoadVocabDecks(root).Decks.Select(d => d.Document).ToList();

        if (decks.Count == 0)
        {
            return;
        }

        var lessonTerms = Loader.LoadLessons(root).Lessons
            .SelectMany(l => l.Document.Vocabulary)
            .Select(v => v.Term)
            .ToList();

        var issues = VocabDeckValidator.ValidateAcross(decks, lessonTerms)
            .Select(i => i.ToString())
            .ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void InRaTienDoDeBietConBaoNhieuTuPhaiSoan()
    {
        var decks = Loader.LoadVocabDecks(FindContentRoot()).Decks.Select(d => d.Document).ToList();
        var total = decks.Sum(d => d.Words.Count);

        output.WriteLine($"Số bộ đã soạn: {decks.Count}");
        output.WriteLine($"Tổng số từ:    {total} / 1000");

        foreach (var deck in decks.OrderBy(d => d.Band))
        {
            output.WriteLine($"  bậc {deck.Band}: {deck.Code} — {deck.Words.Count} từ");
        }
    }

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
