using System.Security.Claims;
using System.Text.Json;
using EnglishForIT.Infrastructure.Learning;
using Microsoft.AspNetCore.Mvc;

namespace EnglishForIT.Api.Modules;

/// <summary>Một câu trả lời gửi lên. Đáp án đúng không nằm trong đây và không bao giờ đi ngược lại.</summary>
public record PlacementAnswerSubmission(
    Guid AttemptId,
    string ItemCode,

    /// <summary>Câu trả lời thô: {"choiceIndex": 2} hoặc {"text": "..."}.</summary>
    JsonElement Response,

    /// <summary>Số giây từ lúc hiện câu tới lúc trả lời. Đầu vào của chỉ số đoán mò.</summary>
    int ResponseSeconds);

public record PlacementSubmitRequest(Guid AttemptId);

/// <summary>
/// Bài xếp lớp.
///
/// Tách khỏi <see cref="LearningModule"/> vì nó không phải một phần của việc học hằng ngày:
/// chạy một lần trước khi lộ trình bắt đầu, có vòng đời riêng, và là nơi duy nhất
/// ghi đè bậc trong hồ sơ học.
/// </summary>
public static class PlacementModule
{
    public static IEndpointRouteBuilder MapPlacementModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/placement").WithTags("Placement");

        group.MapPost("/start", Start)
            .WithSummary("Bắt đầu hoặc mở lại lượt thi. Đề chọn tự động, tránh đề đã thi.");

        group.MapPost("/answer", SubmitAnswer)
            .WithSummary("Nhận một câu trả lời. Chấm tại máy chủ nhưng chỉ trả về tiến độ.");

        group.MapPost("/submit", Submit)
            .WithSummary("Nộp bài: quy ra bậc, đề xuất tầng, ghi vào hồ sơ học.");

        group.MapGet("/result", GetLatestResult)
            .WithSummary("Kết quả lượt gần nhất đã nộp.");

        return app;
    }

    private static async Task<IResult> Start(
        ClaimsPrincipal principal,
        PlacementService placement,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var session = await placement.StartAsync(userId, DateTimeOffset.UtcNow, ct);

        return session is null
            ? Results.NotFound(new
            {
                error = "no_active_form",
                message = "Chưa có đề xếp lớp nào. Bạn vẫn học được từ bài đầu tiên.",
            })
            : Results.Ok(session);
    }

    private static async Task<IResult> SubmitAnswer(
        [FromBody] PlacementAnswerSubmission submission,
        ClaimsPrincipal principal,
        PlacementService placement,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var progress = await placement.SubmitAnswerAsync(
            userId,
            submission.AttemptId,
            submission.ItemCode,
            submission.Response,
            submission.ResponseSeconds,
            DateTimeOffset.UtcNow,
            ct);

        // Một thông báo duy nhất cho cả ba trường hợp: lượt không phải của mình,
        // lượt đã nộp, hoặc mã câu không có trong đề. Nói rõ trường hợp nào là
        // chỉ cho người dò biết mã câu nào có thật.
        return progress is null
            ? Results.NotFound(new
            {
                error = "placement_item_not_found",
                message = "Câu này không thuộc lượt thi đang mở của bạn.",
            })
            : Results.Ok(progress);
    }

    private static async Task<IResult> Submit(
        [FromBody] PlacementSubmitRequest request,
        ClaimsPrincipal principal,
        PlacementService placement,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await placement.SubmitAsync(userId, request.AttemptId, DateTimeOffset.UtcNow, ct);

        return result is null
            ? Results.NotFound(new
            {
                error = "placement_attempt_not_found",
                message = "Không tìm thấy lượt thi này.",
            })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetLatestResult(
        ClaimsPrincipal principal,
        PlacementService placement,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await placement.GetLatestResultAsync(userId, ct);

        return result is null
            ? Results.NotFound(new
            {
                error = "no_placement_result",
                message = "Bạn chưa làm xong bài xếp lớp nào.",
            })
            : Results.Ok(result);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;

        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
