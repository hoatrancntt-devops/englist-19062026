using System.Text;
using System.Text.Json;
using EnglishForIT.Application.Content;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Content;

/// <summary>
/// Ghi ra danh sách những đoạn tiếng Anh cần có file audio, để bước sinh giọng đọc lấy đó làm
/// đầu vào.
///
/// Sinh giọng nằm ngoài API và chạy theo mẻ: nạp model Piper mất khoảng hai giây còn đọc một
/// câu chỉ mất 0,15 giây, nên gọi một lần cho cả nghìn câu nhanh gấp mười lần gọi từng câu.
/// Việc đó không hợp để làm trong tiến trình phục vụ web, nhưng API lại là nơi duy nhất biết
/// nội dung có những câu nào — nên nó ghi danh sách, và script bên ngoài đọc danh sách đó.
///
/// Ghi ra file tạm rồi mới đổi tên. Script có thể chạy đúng lúc API đang khởi động lại, và đọc
/// phải một file mới ghi được nửa chừng thì nó sẽ sinh thiếu mà không báo lỗi gì.
/// </summary>
public class TtsManifestWriter(YamlContentLoader loader, ILogger<TtsManifestWriter> logger)
{
    /// <summary>Thư mục con trong volume media. Script sinh giọng ghi file wav vào đúng đây.</summary>
    public const string DirectoryName = "tts";

    public const string ManifestFileName = "manifest.jsonl";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<int> WriteAsync(string contentRoot, string audioRoot, CancellationToken ct = default)
    {
        var lessons = loader.LoadLessons(contentRoot).Lessons.Select(l => l.Document);
        var placements = loader.LoadPlacementForms(contentRoot).Forms.Select(p => p.Document);
        var roleplays = loader.LoadRoleplayScenarios(contentRoot).Scenarios.Select(r => r.Document);

        var entries = TtsCatalogue.Collect(lessons, placements, roleplays);

        var directory = Path.Combine(audioRoot, DirectoryName);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, ManifestFileName);
        var temporary = path + ".tmp";

        await using (var writer = new StreamWriter(temporary, append: false, new UTF8Encoding(false)))
        {
            foreach (var entry in entries)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(entry, Json).AsMemory(), ct);
            }
        }

        File.Move(temporary, path, overwrite: true);

        var generated = Directory.EnumerateFiles(directory, "*" + TtsCatalogue.FileExtension).Count();

        logger.LogInformation(
            "Danh sách đoạn cần đọc: {Total} đoạn, đã có sẵn {Generated} file. Thiếu {Missing}.",
            entries.Count,
            generated,
            Math.Max(0, entries.Count - generated));

        return entries.Count;
    }
}
