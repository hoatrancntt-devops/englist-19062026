using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Learning;

public record VocabDeckSummary(
    string Code,
    string TitleVi,
    string ContextVi,
    int Band,
    int TotalWords,
    int LearnedWords,
    int DueWords);

/// <summary>Một từ kèm tiến độ của chính học viên đang xem.</summary>
public record VocabWordView(
    string Id,
    string Term,
    string Ipa,
    string MeaningVi,
    string Chunk,
    string? Emoji,
    string? MnemonicVi,
    double BestScore,
    bool Learned,
    bool Due);

public record VocabDeckView(
    string Code,
    string TitleVi,
    string ContextVi,
    int Band,
    int PassScore,
    IReadOnlyList<VocabWordView> Words);

public record VocabWordResult(
    bool Passed,
    double Score,
    int PassThreshold,
    string MessageVi,
    int NextReviewInDays);

/// <summary>
/// Bộ từ vựng tần suất cao: liệt kê bộ, mở bộ, và chấm từng từ.
///
/// CỐ Ý không có quyền mở khoá gì trên lộ trình. Bắt học viên qua 1.000 từ mới được học bài
/// tiếp theo là chặn đứng cả trăm bài đang có — người đang học dở sẽ mất đường đi. Bộ từ vựng
/// chạy song song: ai muốn dựng vốn từ thì vào, không ai bị nó khoá lại.
/// </summary>
public class VocabDeckService(
    AppDbContext db,
    ILogger<VocabDeckService> logger)
{
    /// <summary>Ngưỡng nói lại coi là thuộc. Bằng ngưỡng của mọi bước trong bài học.</summary>
    private const int PassThreshold = 80;

    /// <summary>Trần giãn cách. Xa hơn hai tháng thì lần ôn đó gần như không còn tác dụng nhắc.</summary>
    private const int MaxIntervalDays = 60;

    public async Task<IReadOnlyList<VocabDeckSummary>> GetDecksAsync(
        Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var decks = await db.VocabDecks
            .AsNoTracking()
            .OrderBy(d => d.Band)
            .Select(d => new
            {
                d.Id,
                d.Code,
                d.TitleVi,
                d.ContextVi,
                d.Band,
                Total = d.Words.Count,
            })
            .ToListAsync(ct);

        if (decks.Count == 0)
        {
            return [];
        }

        // Một truy vấn cho toàn bộ tiến độ thay vì một truy vấn mỗi bộ.
        var progress = await db.VocabWordProgresses
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Word!.DeckId, p.FirstLearnedAt, p.DueAt })
            .ToListAsync(ct);

        var byDeck = progress.GroupBy(p => p.DeckId).ToDictionary(g => g.Key, g => g.ToList());

        return [.. decks.Select(d =>
        {
            var rows = byDeck.GetValueOrDefault(d.Id, []);

            return new VocabDeckSummary(
                d.Code,
                d.TitleVi,
                d.ContextVi,
                d.Band,
                d.Total,
                rows.Count(r => r.FirstLearnedAt != null),
                rows.Count(r => r.FirstLearnedAt != null && r.DueAt <= now));
        })];
    }

    public async Task<VocabDeckView?> GetDeckAsync(
        Guid userId, string code, DateTimeOffset now, CancellationToken ct = default)
    {
        var deck = await db.VocabDecks
            .AsNoTracking()
            .Include(d => d.Words)
            .FirstOrDefaultAsync(d => d.Code == code, ct);

        if (deck is null)
        {
            return null;
        }

        var wordIds = deck.Words.Select(w => w.Id).ToList();

        var progress = await db.VocabWordProgresses
            .AsNoTracking()
            .Where(p => p.UserId == userId && wordIds.Contains(p.WordId))
            .ToDictionaryAsync(p => p.WordId, ct);

        var words = deck.Words
            .OrderBy(w => w.OrderIndex)
            .Select(w =>
            {
                var row = progress.GetValueOrDefault(w.Id);

                return new VocabWordView(
                    w.Id.ToString(),
                    w.Term,
                    w.Ipa,
                    w.MeaningVi,
                    w.Chunk,
                    w.Emoji,
                    w.MnemonicVi,
                    row?.BestScore ?? 0,
                    row?.FirstLearnedAt != null,
                    row is not null && row.FirstLearnedAt != null && row.DueAt <= now);
            })
            .ToList();

        return new VocabDeckView(deck.Code, deck.TitleVi, deck.ContextVi, deck.Band, PassThreshold, words);
    }

    /// <summary>
    /// Chấm một từ từ những lần thu giọng ĐÃ được máy chủ chấm và lưu.
    ///
    /// Cùng đường với bước nói của bài học: client gửi file ghi âm tới /speech/grade kèm
    /// contextId là id của từ, máy chủ chấm và lưu, rồi chỗ này đọc lại. Điểm không đi qua
    /// trình duyệt một lần nào.
    /// </summary>
    public async Task<VocabWordResult?> RecordAsync(
        Guid userId, Guid wordId, DateTimeOffset now, CancellationToken ct = default)
    {
        var word = await db.VocabWords.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wordId, ct);

        if (word is null)
        {
            return null;
        }

        var best = await db.SpeechAttempts
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.ContextId == wordId)
            .Select(a => new { a.PronunciationScore, a.FluencyScore, a.CommunicationScore })
            .ToListAsync(ct);

        if (best.Count == 0)
        {
            return new VocabWordResult(
                false, 0, PassThreshold,
                "Bạn chưa thu âm từ này. Nghe mẫu rồi bấm thu, máy sẽ chấm ngay.",
                0);
        }

        var score = best
            .Select(a => SpeechScorer.Overall(a.PronunciationScore, a.FluencyScore, a.CommunicationScore))
            .Max();

        var passed = score >= PassThreshold;

        var row = await db.VocabWordProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.WordId == wordId, ct);

        if (row is null)
        {
            row = new VocabWordProgress { UserId = userId, WordId = wordId };
            db.VocabWordProgresses.Add(row);
        }

        row.AttemptCount++;
        row.LastSeenAt = now;

        // Điểm chỉ đi lên. Ôn lại một từ đã thuộc mà lỡ nói vấp không được xoá công cũ.
        row.BestScore = Math.Max(row.BestScore, score);

        if (passed)
        {
            row.FirstLearnedAt ??= now;

            // Giãn dần theo hệ số, cùng quy ước với hàng ôn tập của bài học.
            row.IntervalDays = Math.Min(MaxIntervalDays, Math.Max(1, (int)Math.Round(row.IntervalDays * row.Ease)));
            row.Ease = Math.Min(2.8, row.Ease + 0.1);
        }
        else
        {
            // Sai thì kéo về một ngày và hạ hệ số: từ này cần gặp lại sớm.
            row.IntervalDays = 1;
            row.Ease = Math.Max(1.3, row.Ease - 0.2);
        }

        row.DueAt = now.AddDays(row.IntervalDays);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Học viên {UserId} nói từ {Term}: {Score} điểm, hẹn lại sau {Days} ngày",
            userId, word.Term, score, row.IntervalDays);

        return new VocabWordResult(
            passed,
            score,
            PassThreshold,
            passed
                ? $"{score} điểm — thuộc rồi. Gặp lại từ này sau {row.IntervalDays} ngày."
                : $"{score} điểm, chưa đủ {PassThreshold}. Nghe lại mẫu rồi thu thêm lần nữa.",
            row.IntervalDays);
    }
}
