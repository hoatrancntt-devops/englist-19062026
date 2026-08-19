using System.Security.Claims;
using EnglishForIT.Infrastructure.Learning;
using Microsoft.AspNetCore.Mvc;

namespace EnglishForIT.Api.Modules;

/// <summary>Một lựa chọn được nộp. Client gửi chỉ số, chất lượng ở lại máy chủ.</summary>
public record RoleplayChoiceSubmission(Guid AttemptId, string NodeCode, int ChoiceIndex);

/// <summary>
/// Roleplay.
///
/// Cùng ràng buộc với mọi phần chấm khác: nhãn chất lượng và lời giải thích không rời máy chủ
/// trước khi học viên chọn. Thấy trước nhãn "good" thì bài này thành trò bấm nhãn.
/// </summary>
public static class RoleplayModule
{
    public static IEndpointRouteBuilder MapRoleplayModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/roleplay").WithTags("Roleplay");

        group.MapGet("/", List)
            .WithSummary("Danh sách kịch bản kèm kết quả lần chơi gần nhất");

        group.MapPost("/{code}/start", Start)
            .WithSummary("Bắt đầu một lượt mới. Lượt dở cũ của cùng kịch bản bị bỏ.");

        group.MapPost("/choose", Choose)
            .WithSummary("Chọn một câu đáp và đi tới lượt kế");

        return app;
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal, RoleplayService roleplay, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await roleplay.ListAsync(userId, ct));
    }

    private static async Task<IResult> Start(
        string code, ClaimsPrincipal principal, RoleplayService roleplay, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var start = await roleplay.StartAsync(userId, code, DateTimeOffset.UtcNow, ct);

        return start is null
            ? Results.NotFound(new { error = "scenario_not_found", message = "Không có kịch bản nào mang mã này." })
            : Results.Ok(start);
    }

    private static async Task<IResult> Choose(
        [FromBody] RoleplayChoiceSubmission submission,
        ClaimsPrincipal principal,
        RoleplayService roleplay,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await roleplay.ChooseAsync(
            userId, submission.AttemptId, submission.NodeCode, submission.ChoiceIndex, DateTimeOffset.UtcNow, ct);

        // Một câu trả lời duy nhất cho mọi nguyên nhân: lượt lạ, node lạ, chỉ số ngoài phạm vi.
        // Phân biệt chúng là mở đường dò cấu trúc kịch bản.
        return result is null
            ? Results.NotFound(new { error = "choice_not_found", message = "Lựa chọn này không thuộc lượt chơi đang mở của bạn." })
            : Results.Ok(result);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
