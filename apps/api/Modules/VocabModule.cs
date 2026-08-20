using System.Security.Claims;
using EnglishForIT.Infrastructure.Learning;

namespace EnglishForIT.Api.Modules;

/// <summary>
/// Bộ từ vựng tần suất cao.
///
/// Tách khỏi LearningModule vì nó không đụng tới lộ trình: không mở khoá bài, không tính vào
/// mastery. Gộp chung thì hai thứ có vòng đời khác hẳn nhau nằm trong một file.
/// </summary>
public static class VocabModule
{
    public static IEndpointRouteBuilder MapVocabModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/vocab").WithTags("Vocab");

        group.MapGet("/", GetDecks)
            .WithSummary("Danh sách bộ từ vựng kèm tiến độ");

        group.MapGet("/{code}", GetDeck)
            .WithSummary("Một bộ từ vựng kèm tiến độ từng từ");

        group.MapPost("/words/{wordId:guid}", RecordWord)
            .WithSummary("Chấm một từ từ bản ghi giọng đã lưu, và hẹn lịch ôn lại");

        return app;
    }

    private static async Task<IResult> GetDecks(
        ClaimsPrincipal principal,
        VocabDeckService vocab,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await vocab.GetDecksAsync(userId, DateTimeOffset.UtcNow, ct));
    }

    private static async Task<IResult> GetDeck(
        string code,
        ClaimsPrincipal principal,
        VocabDeckService vocab,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var deck = await vocab.GetDeckAsync(userId, code, DateTimeOffset.UtcNow, ct);

        return deck is null
            ? Results.NotFound(new { error = "deck_not_found", message = $"Không có bộ từ vựng {code}." })
            : Results.Ok(deck);
    }

    private static async Task<IResult> RecordWord(
        Guid wordId,
        ClaimsPrincipal principal,
        VocabDeckService vocab,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await vocab.RecordAsync(userId, wordId, DateTimeOffset.UtcNow, ct);

        return result is null
            ? Results.NotFound(new { error = "word_not_found", message = "Không có từ này." })
            : Results.Ok(result);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
