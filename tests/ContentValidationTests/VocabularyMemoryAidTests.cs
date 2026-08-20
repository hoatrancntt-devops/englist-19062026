using EnglishForIT.Application.Content;
using EnglishForIT.Infrastructure.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace EnglishForIT.ContentValidationTests;

/// <summary>
/// Độ phủ của phần hỗ trợ ghi nhớ từ vựng: emoji và mẹo nhớ kiểu keyword.
///
/// CỐ Ý không bắt buộc. 609 mục từ vựng đã có sẵn trước khi hai trường này ra đời, và soạn
/// chúng là việc dài hơi — bắt buộc ngay thì CI đỏ suốt nhiều ngày và người soạn mất luôn
/// tín hiệu về những lỗi thật.
///
/// Thay vào đó test in ra con số phủ mỗi lần chạy, và chỉ chặn những lỗi thật sự sai: mẹo nhớ
/// rỗng, hoặc emoji dài bất thường (dấu hiệu ai đó nhét cả câu vào ô emoji).
/// </summary>
public class VocabularyMemoryAidTests(ITestOutputHelper output)
{
    private static readonly LoadResult Loaded = LoadAll();

    [Fact]
    public void InRaDoPhuDeBietConBaoNhieuTuChuaSoan()
    {
        var words = Loaded.Lessons.SelectMany(l => l.Document.Vocabulary).ToList();

        var withEmoji = words.Count(w => !string.IsNullOrWhiteSpace(w.Emoji));
        var withMnemonic = words.Count(w => !string.IsNullOrWhiteSpace(w.MnemonicVi));

        output.WriteLine($"Tổng số mục từ vựng: {words.Count}");
        output.WriteLine($"Có emoji:   {withEmoji} ({100.0 * withEmoji / words.Count:N1}%)");
        output.WriteLine($"Có mẹo nhớ: {withMnemonic} ({100.0 * withMnemonic / words.Count:N1}%)");

        Assert.NotEmpty(words);
    }

    [Fact]
    public void MeoNhoKhongDuocRong()
    {
        // Khai trường rồi để chuỗi rỗng thì giao diện hiện nút "Mẹo nhớ" mở ra một khoảng trắng.
        foreach (var lesson in Loaded.Lessons)
        {
            foreach (var word in lesson.Document.Vocabulary)
            {
                Assert.False(word.MnemonicVi is not null && word.MnemonicVi.Trim().Length == 0,
                    $"{lesson.Document.Code}/{word.Term}: mnemonic_vi khai rỗng.");

                Assert.False(word.Emoji is not null && word.Emoji.Trim().Length == 0,
                    $"{lesson.Document.Code}/{word.Term}: emoji khai rỗng.");
            }
        }
    }

    [Fact]
    public void OEmojiChiChuaEmojiChuKhongChuaCauChu()
    {
        // Ngưỡng rộng tay: một emoji ghép gia đình hay cờ có thể dài hơn chục ký tự.
        const int maxLength = 16;

        foreach (var lesson in Loaded.Lessons)
        {
            foreach (var word in lesson.Document.Vocabulary.Where(w => w.Emoji is not null))
            {
                Assert.True(word.Emoji!.Length <= maxLength,
                    $"{lesson.Document.Code}/{word.Term}: ô emoji dài {word.Emoji.Length} ký tự, có vẻ là câu chữ chứ không phải emoji.");
            }
        }
    }

    [Fact]
    public void MeoNhoPhaiDuDaiDeCoNghia()
    {
        // Mẹo nhớ kiểu keyword cần một từ nghe giống VÀ một câu nối. Dưới 20 ký tự thì
        // chắc chắn thiếu một trong hai, và mẹo nửa vời không giúp nhớ được gì.
        const int minLength = 20;

        foreach (var lesson in Loaded.Lessons)
        {
            foreach (var word in lesson.Document.Vocabulary
                .Where(w => !string.IsNullOrWhiteSpace(w.MnemonicVi)))
            {
                Assert.True(word.MnemonicVi!.Trim().Length >= minLength,
                    $"{lesson.Document.Code}/{word.Term}: mẹo nhớ chỉ {word.MnemonicVi.Trim().Length} ký tự, quá ngắn để có nghĩa.");
            }
        }
    }

    private static LoadResult LoadAll()
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
