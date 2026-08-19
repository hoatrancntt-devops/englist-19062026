using System.Security.Claims;
using EnglishForIT.Infrastructure.Learning;

namespace EnglishForIT.Api.Modules;

/// <summary>
/// Chấm phát âm.
///
/// Nhận file ghi âm và câu mẫu, trả điểm ba trục kèm nhận xét tiếng Việt.
///
/// Giới hạn cần nói rõ với người dùng API: chấm ở mức TỪ chứ không phải âm vị. Hệ thống biết
/// học viên có nói ra đúng từ hay không, không biết họ phát âm âm nào sai.
/// </summary>
public static class SpeechModule
{
    /// <summary>Trần kích thước file. Một câu đọc theo mẫu dài nhất cũng chỉ vài trăm KB.</summary>
    private const long MaxAudioBytes = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapSpeechModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/speech").WithTags("Speech");

        group.MapGet("/status", Status)
            .WithSummary("Phần chấm phát âm có đang bật không");

        group.MapPost("/grade", Grade)
            .RequireRateLimiting("speech")
            .DisableAntiforgery()
            .WithSummary("Chấm một lượt nói. Giọng không rời máy chủ.");

        return app;
    }

    private static IResult Status(SpeechService speech) =>
        Results.Ok(new
        {
            enabled = speech.Enabled,
            messageVi = speech.Enabled
                ? "Chấm ở mức từ: hệ thống biết bạn có nói đúng từ hay không, chưa phân tích được từng âm."
                : "Phần chấm phát âm chưa bật trên máy chủ này.",
        });

    private static async Task<IResult> Grade(
        HttpRequest request,
        ClaimsPrincipal principal,
        SpeechService speech,
        CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Results.Unauthorized();
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "expected_multipart", message = "Cần gửi dạng multipart kèm file ghi âm." });
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("audio");

        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "missing_audio", message = "Thiếu file ghi âm." });
        }

        if (file.Length > MaxAudioBytes)
        {
            return Results.BadRequest(new
            {
                error = "audio_too_large",
                message = "File ghi âm quá lớn. Mỗi lượt nói chỉ nên dài vài giây.",
            });
        }

        var expectedText = form["expectedText"].ToString();

        if (string.IsNullOrWhiteSpace(expectedText))
        {
            return Results.BadRequest(new { error = "missing_expected_text", message = "Thiếu câu mẫu để đối chiếu." });
        }

        var contextType = form["contextType"].ToString() is { Length: > 0 } ctx ? ctx : "lesson_activity";
        Guid? contextId = Guid.TryParse(form["contextId"].ToString(), out var id) ? id : null;
        var durationMs = int.TryParse(form["durationMs"].ToString(), out var ms) ? ms : 0;

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);

        var grade = await speech.GradeAsync(
            userId, contextType, contextId, expectedText, buffer.ToArray(), file.FileName,
            durationMs, DateTimeOffset.UtcNow, ct);

        return Results.Ok(grade);
    }
}
