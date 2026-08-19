using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Content;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnglishForIT.ContentValidationTests;

/// <summary>
/// Cổng chất lượng chạy trên đề xếp lớp thật trong content/placement.
///
/// Đề sai nguy hiểm hơn bài học sai: bài học sai làm hỏng một buổi, đề sai đặt
/// nhầm cả lộ trình phía sau và không ai phát hiện cho tới khi học viên bỏ cuộc.
/// </summary>
public class PlacementQualityGateTests
{
    private static readonly PlacementLoadResult Loaded = LoadAll();
    private static readonly PlacementValidator Validator = new();

    [Fact]
    public void MoiFileDeDocDuoc()
    {
        Assert.Empty(Loaded.Errors.Select(e => $"{Path.GetFileName(e.FilePath)}: {e.Message}"));
    }

    [Fact]
    public void CoItNhatHaiDe()
    {
        // Một đề thì chống thi lại gặp đề cũ không hoạt động, và test bên dưới
        // sẽ pass một cách vô nghĩa.
        Assert.True(Loaded.Forms.Count >= 2,
            $"Chỉ có {Loaded.Forms.Count} đề. Cần tối thiểu 2 đề song song.");
    }

    [Fact]
    public void ToanBoDeQuaCongChatLuong()
    {
        var docs = Loaded.Forms.Select(f => f.Document).ToList();

        var issues = docs.SelectMany(Validator.ValidateOne)
            .Concat(Validator.ValidateSet(docs))
            .Select(i => i.ToString());

        Assert.Empty(issues);
    }

    [Fact]
    public void HaiDeSongSongCoCungPhanBoKyNang()
    {
        // Đề A dễ hơn đề B thì người thi lại ra bậc khác không phải vì trình độ đổi
        // mà vì bốc phải đề dễ hơn. Đó là lỗi không ai nhìn số liệu mà thấy được.
        var distributions = Loaded.Forms
            .Select(f => new
            {
                f.Document.Code,
                BySkill = f.Document.Items
                    .GroupBy(i => i.Skill?.ToString() ?? i.Kind.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
            })
            .ToList();

        var reference = distributions[0];

        foreach (var other in distributions.Skip(1))
        {
            Assert.Equal(reference.BySkill, other.BySkill);
        }
    }

    [Fact]
    public void HaiDeSongSongCoCungBacDoKhoOTungViTri()
    {
        var byOrder = Loaded.Forms
            .Select(f => f.Document.Items.Select(i => i.Difficulty).ToList())
            .ToList();

        var reference = byOrder[0];

        foreach (var other in byOrder.Skip(1))
        {
            Assert.Equal(reference, other);
        }
    }

    [Fact]
    public void KhongDeNaoLoDapAnQuaPhanHienThi()
    {
        // Toàn bộ prompt được serialize và trả ra client. Lỡ tay đặt đáp án vào
        // một trường của prompt là rò cả đề mà không có gì báo.
        foreach (var form in Loaded.Forms)
        {
            foreach (var item in form.Document.Items.Where(i => i.Answer?.Accepted is { Count: > 0 }))
            {
                var visible = string.Join(" ",
                    item.Prompt.SentenceEn ?? string.Empty,
                    item.Prompt.QuestionEn ?? string.Empty,
                    item.Prompt.PassageEn ?? string.Empty,
                    string.Join(" ", item.Prompt.Choices ?? []));

                foreach (var accepted in item.Answer!.Accepted!)
                {
                    Assert.False(
                        visible.Contains(accepted, StringComparison.OrdinalIgnoreCase),
                        $"{form.Document.Code}/{item.Code}: đáp án \"{accepted}\" xuất hiện trong phần hiển thị.");
                }
            }
        }
    }

    [Fact]
    public void MoiCauNghePhaiCoAudio()
    {
        var missing = Loaded.Forms
            .SelectMany(f => f.Document.Items.Select(i => (Form: f.Document.Code, Item: i)))
            .Where(x => x.Item.Skill == SkillType.Listening && string.IsNullOrWhiteSpace(x.Item.Prompt.AudioText))
            .Select(x => $"{x.Form}/{x.Item.Code}");

        Assert.Empty(missing);
    }

    [Fact]
    public void HashOnDinhGiuaHaiLanDoc()
    {
        var again = LoadAll();

        foreach (var form in Loaded.Forms)
        {
            var match = again.Forms.Single(f => f.Document.Code == form.Document.Code);
            Assert.Equal(form.SourceHash, match.SourceHash);
        }
    }

    private static PlacementLoadResult LoadAll()
    {
        var loader = new YamlContentLoader(NullLogger<YamlContentLoader>.Instance);
        return loader.LoadPlacementForms(FindContentRoot());
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

        throw new DirectoryNotFoundException("Không tìm thấy thư mục content/ từ thư mục chạy test.");
    }
}
