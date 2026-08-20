using System.Security.Cryptography;
using System.Text;
using EnglishForIT.Application.Content;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EnglishForIT.Infrastructure.Content;

/// <summary>Một file nội dung đã đọc được, kèm hash của nội dung gốc.</summary>
public record LoadedLesson(LessonDocument Document, string SourceHash, string FilePath);

/// <summary>Một đề xếp lớp đã đọc được, kèm hash của nội dung gốc.</summary>
public record LoadedPlacement(PlacementDocument Document, string SourceHash, string FilePath);

/// <summary>Lỗi đọc file: YAML sai cú pháp, hoặc thiếu trường mà bộ chuyển đổi không dựng nổi đối tượng.</summary>
public record LoadError(string FilePath, string Message);

public record LoadResult(IReadOnlyList<LoadedLesson> Lessons, IReadOnlyList<LoadError> Errors);

public record PlacementLoadResult(IReadOnlyList<LoadedPlacement> Forms, IReadOnlyList<LoadError> Errors);

/// <summary>Một kịch bản roleplay đã đọc, kèm hash để seeder biết có đổi hay không.</summary>
public record LoadedRoleplay(RoleplayDocument Document, string SourceHash, string FilePath);

public record RoleplayLoadResult(IReadOnlyList<LoadedRoleplay> Scenarios, IReadOnlyList<LoadError> Errors);

/// <summary>Một chương truyện đã đọc, kèm hash để seeder biết có đổi hay không.</summary>
public record LoadedStory(StoryDocument Document, string SourceHash, string FilePath);

public record StoryLoadResult(IReadOnlyList<LoadedStory> Chapters, IReadOnlyList<LoadError> Errors);

/// <summary>Một bộ bài luyện viết đã đọc, kèm hash để seeder biết có đổi hay không.</summary>
public record LoadedWriting(WritingSetDocument Document, string SourceHash, string FilePath);

public record WritingLoadResult(IReadOnlyList<LoadedWriting> Sets, IReadOnlyList<LoadError> Errors);

public record LoadedVocabDeck(VocabDeckDocument Document, string SourceHash, string FilePath);

public record VocabDeckLoadResult(IReadOnlyList<LoadedVocabDeck> Decks, IReadOnlyList<LoadError> Errors);

/// <summary>
/// Đọc content/lessons/**/*.yaml thành đối tượng.
///
/// Hash nội dung gốc để seeder bỏ qua file không đổi — nạp lại toàn bộ thư mục
/// vẫn nhanh vì phần lớn file không có gì mới.
/// </summary>
public class YamlContentLoader(ILogger<YamlContentLoader> logger)
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        // YAML viết snake_case cho dễ đọc, C# nhận PascalCase.
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        // Trường lạ trong YAML bị bỏ qua thay vì ném lỗi: thêm trường mới cho công cụ soạn thảo
        // không được làm hỏng seeder đang chạy.
        .IgnoreUnmatchedProperties()
        .WithEnforceNullability()
        .Build();

    public LoadResult LoadLessons(string contentRoot)
    {
        var (docs, errors) = LoadFolder<LessonDocument>(contentRoot, "lessons");

        return new LoadResult(
            docs.Select(d => new LoadedLesson(d.Document, d.Hash, d.FilePath)).ToList(),
            errors);
    }

    public PlacementLoadResult LoadPlacementForms(string contentRoot)
    {
        var (docs, errors) = LoadFolder<PlacementDocument>(contentRoot, "placement");

        return new PlacementLoadResult(
            docs.Select(d => new LoadedPlacement(d.Document, d.Hash, d.FilePath)).ToList(),
            errors);
    }

    public RoleplayLoadResult LoadRoleplayScenarios(string contentRoot)
    {
        var (docs, errors) = LoadFolder<RoleplayDocument>(contentRoot, "roleplay");

        return new RoleplayLoadResult(
            docs.Select(d => new LoadedRoleplay(d.Document, d.Hash, d.FilePath)).ToList(),
            errors);
    }

    public StoryLoadResult LoadStoryChapters(string contentRoot)
    {
        var (docs, errors) = LoadFolder<StoryDocument>(contentRoot, "story");

        return new StoryLoadResult(
            docs.Select(d => new LoadedStory(d.Document, d.Hash, d.FilePath)).ToList(),
            errors);
    }

    public WritingLoadResult LoadWritingSets(string contentRoot)
    {
        var (docs, errors) = LoadFolder<WritingSetDocument>(contentRoot, "writing");

        return new WritingLoadResult(
            docs.Select(d => new LoadedWriting(d.Document, d.Hash, d.FilePath)).ToList(),
            errors);
    }

    public VocabDeckLoadResult LoadVocabDecks(string contentRoot)
    {
        var (docs, errors) = LoadFolder<VocabDeckDocument>(contentRoot, "vocab");

        return new VocabDeckLoadResult(
            docs.Select(d => new LoadedVocabDeck(d.Document, d.Hash, d.FilePath)).ToList(),
            errors);
    }

    private record LoadedFile<T>(T Document, string Hash, string FilePath);

    private (List<LoadedFile<T>> Documents, List<LoadError> Errors) LoadFolder<T>(string contentRoot, string folder)
        where T : class
    {
        var root = Path.Combine(contentRoot, folder);
        var loaded = new List<LoadedFile<T>>();
        var errors = new List<LoadError>();

        if (!Directory.Exists(root))
        {
            logger.LogWarning("Không tìm thấy thư mục nội dung: {Path}", root);
            return (loaded, errors);
        }

        var files = Directory
            .EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files)
        {
            try
            {
                var text = File.ReadAllText(file);
                var doc = _deserializer.Deserialize<T>(text);

                if (doc is null)
                {
                    errors.Add(new LoadError(file, "File rỗng hoặc không phải YAML hợp lệ."));
                    continue;
                }

                loaded.Add(new LoadedFile<T>(doc, ComputeHash(text), file));
            }
            catch (YamlException ex)
            {
                // Báo đúng dòng để người soạn tìm được chỗ sai, thay vì "YAML lỗi".
                errors.Add(new LoadError(file,
                    $"Dòng {ex.Start.Line}, cột {ex.Start.Column}: {ex.InnerException?.Message ?? ex.Message}"));
            }
            catch (Exception ex)
            {
                errors.Add(new LoadError(file, ex.Message));
            }
        }

        logger.LogInformation("Đọc được {Count} file từ {Root}, {Errors} file lỗi",
            loaded.Count, root, errors.Count);

        return (loaded, errors);
    }

    private static string ComputeHash(string content)
    {
        // Chuẩn hoá xuống dòng trước khi hash: cùng nội dung mà checkout trên Windows
        // và Linux không được ra hai hash khác nhau.
        var normalized = content.Replace("\r\n", "\n");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
