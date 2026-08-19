using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EnglishForIT.Infrastructure.Learning;
using Microsoft.AspNetCore.Mvc;

namespace EnglishForIT.Api.Modules;

/// <summary>
/// Bài nộp.
///
/// Một mảng chuỗi cho cả ba dạng, giống hệt bước viết trong bài học: điền chỗ trống thì mỗi
/// phần tử là một ô, sắp câu thì mỗi phần tử là một mảnh theo thứ tự học viên chọn, viết email
/// thì một phần tử duy nhất. Dạng bài do máy chủ tra từ mã, client không khai và cũng không được khai.
/// </summary>
public record WritingSubmissionRequest(
    [Required] string TaskCode,
    IReadOnlyList<string>? Answers);

/// <summary>
/// Bộ bài luyện viết.
///
/// Chấm hoàn toàn tại máy chủ bằng luật. Nhận xét chi tiết chỉ sinh sau khi nộp, giống mọi
/// phần chấm khác của hệ thống.
/// </summary>
public static class WritingModule
{
    public static IEndpointRouteBuilder MapWritingModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/writing").WithTags("Writing");

        group.MapGet("/", List)
            .WithSummary("Danh sách bộ bài kèm số bài đã đạt");

        group.MapGet("/{code}", GetSet)
            .WithSummary("Đề của một bộ. Không kèm đáp án.");

        group.MapPost("/{code}/submit", Submit)
            .WithSummary("Nộp một bài và nhận điểm kèm nhận xét từng ý");

        return app;
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal, WritingService writing, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await writing.ListAsync(userId, ct));
    }

    private static async Task<IResult> GetSet(
        string code, ClaimsPrincipal principal, WritingService writing, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var set = await writing.GetSetAsync(userId, code, ct);

        return set is null
            ? Results.NotFound(new { error = "set_not_found", message = "Không có bộ bài nào mang mã này." })
            : Results.Ok(set);
    }

    private static async Task<IResult> Submit(
        string code,
        [FromBody] WritingSubmissionRequest request,
        ClaimsPrincipal principal,
        WritingService writing,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await writing.SubmitAsync(
            userId, code, request.TaskCode, request.Answers ?? [], DateTimeOffset.UtcNow, ct);

        return result is null
            ? Results.NotFound(new { error = "task_not_found", message = "Không có bài nào mang mã này trong bộ." })
            : Results.Ok(result);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
