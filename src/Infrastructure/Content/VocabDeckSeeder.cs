using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Content;

/// <summary>
/// Nạp bộ từ vựng tần suất cao từ content/vocab/*.yaml.
///
/// Cùng nguyên tắc với seeder bài viết: <c>vocab_word_progress</c> có khoá ngoại trỏ vào
/// <c>vocab_words</c>, nên xoá rồi dựng lại từ sẽ cuốn theo toàn bộ tiến độ học viên. Vì vậy
/// từ được cập nhật TẠI CHỖ theo term, chỉ xoá những từ đã bị gỡ khỏi file.
/// </summary>
public class VocabDeckSeeder(
    AppDbContext db,
    YamlContentLoader loader,
    ILogger<VocabDeckSeeder> logger)
{
    public async Task<SeedReport> SeedAsync(string contentRoot, CancellationToken ct = default)
    {
        var problems = new List<string>();

        var load = loader.LoadVocabDecks(contentRoot);
        problems.AddRange(load.Errors.Select(e =>
            $"Doc bo tu vung that bai {Path.GetFileName(e.FilePath)}: {e.Message}"));

        var docs = load.Decks.Select(d => d.Document).ToList();

        if (docs.Count == 0)
        {
            logger.LogInformation("Chưa có bộ từ vựng nào trong {Root}/vocab", contentRoot);
            return new SeedReport(0, 0, 0, 0, problems);
        }

        var issues = docs.SelectMany(VocabDeckValidator.Validate).ToList();

        // Kiểm chéo cần biết những từ nào bài học đã dạy, nếu không bộ từ vựng sẽ lặp lại
        // chúng và con số "từ mới" thành sai.
        var lessonTerms = await db.LessonActivities
            .AsNoTracking()
            .Where(a => a.Kind == Domain.Enums.ActivityKind.Vocab)
            .Select(a => a.PayloadJson)
            .ToListAsync(ct);

        issues.AddRange(VocabDeckValidator.ValidateAcross(docs, ExtractTerms(lessonTerms)));

        if (issues.Count > 0)
        {
            problems.AddRange(issues.Select(i => i.ToString()));
            logger.LogError("Bộ từ vựng không qua cổng chất lượng, huỷ seed. {Count} vấn đề", issues.Count);

            return new SeedReport(0, 0, 0, load.Decks.Count, problems);
        }

        var existing = await db.VocabDecks
            .Include(d => d.Words)
            .ToDictionaryAsync(d => d.Code, StringComparer.OrdinalIgnoreCase, ct);

        int inserted = 0, updated = 0, unchanged = 0;

        foreach (var loaded in load.Decks)
        {
            var doc = loaded.Document;

            if (existing.TryGetValue(doc.Code, out var deck))
            {
                if (deck.SourceHash == loaded.SourceHash)
                {
                    unchanged++;
                    continue;
                }

                Apply(deck, doc, loaded.SourceHash);
                SyncWords(deck, doc);
                updated++;
                continue;
            }

            var created = new VocabDeck
            {
                Code = doc.Code,
                TitleVi = doc.TitleVi,
                ContextVi = doc.ContextVi,
                Band = doc.Band,
                SourceHash = loaded.SourceHash,
            };

            SyncWords(created, doc);
            db.VocabDecks.Add(created);
            inserted++;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Bộ từ vựng: {Inserted} mới, {Updated} cập nhật, {Unchanged} không đổi",
            inserted, updated, unchanged);

        return new SeedReport(inserted, updated, unchanged, 0, problems);
    }

    private static void Apply(VocabDeck deck, VocabDeckDocument doc, string hash)
    {
        deck.TitleVi = doc.TitleVi;
        deck.ContextVi = doc.ContextVi;
        deck.Band = doc.Band;
        deck.SourceHash = hash;
    }

    /// <summary>
    /// Đồng bộ danh sách từ mà không xoá dòng còn dùng được.
    ///
    /// Từ khớp theo <c>Term</c> thì cập nhật tại chỗ, giữ nguyên Id — nhờ vậy tiến độ học viên
    /// trỏ vào Id đó vẫn còn. Chỉ từ đã bị gỡ khỏi file mới bị xoá.
    /// </summary>
    private static void SyncWords(VocabDeck deck, VocabDeckDocument doc)
    {
        var byTerm = deck.Words.ToDictionary(w => w.Term, StringComparer.OrdinalIgnoreCase);
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < doc.Words.Count; index++)
        {
            var source = doc.Words[index];
            keep.Add(source.Term);

            if (byTerm.TryGetValue(source.Term, out var word))
            {
                word.Ipa = source.Ipa;
                word.MeaningVi = source.MeaningVi;
                word.Chunk = source.Chunk;
                word.Emoji = source.Emoji;
                word.MnemonicVi = source.MnemonicVi;
                word.OrderIndex = index;
                continue;
            }

            deck.Words.Add(new VocabWord
            {
                Term = source.Term,
                Ipa = source.Ipa,
                MeaningVi = source.MeaningVi,
                Chunk = source.Chunk,
                Emoji = source.Emoji,
                MnemonicVi = source.MnemonicVi,
                OrderIndex = index,
            });
        }

        deck.Words.RemoveAll(w => !keep.Contains(w.Term));
    }

    /// <summary>Bóc danh sách term ra khỏi payload của các bước từ vựng trong bài học.</summary>
    private static List<string> ExtractTerms(IEnumerable<string> payloads)
    {
        var terms = new List<string>();

        foreach (var payload in payloads)
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(payload);

                if (!document.RootElement.TryGetProperty("Vocabulary", out var words)
                    || words.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    continue;
                }

                terms.AddRange(words.EnumerateArray()
                    .Select(w => w.TryGetProperty("Term", out var t) ? t.GetString() : null)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t!));
            }
            catch (System.Text.Json.JsonException)
            {
                // Payload hỏng là việc của cổng kiểm định bài học, không phải của seeder này.
            }
        }

        return terms;
    }
}
