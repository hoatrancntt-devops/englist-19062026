using System.Security.Claims;
using EnglishForIT.Infrastructure.Learning;

namespace EnglishForIT.Api.Modules;

/// <summary>
/// Mạch truyện.
///
/// Chương chưa mở trả về tiêu đề, câu mở và mốc cần đạt — vừa đủ để học viên biết phải học
/// gì để mở. Thân chương ở lại máy chủ cho tới khi mốc đạt thật.
/// </summary>
public static class StoryModule
{
    public static IEndpointRouteBuilder MapStoryModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/story").WithTags("Story");

        group.MapGet("/", List)
            .WithSummary("Danh sách chương kèm trạng thái mở của học viên");

        group.MapGet("/{code}", Read)
            .WithSummary("Đọc một chương đã mở. Lần đọc đầu tiên được ghi mốc.");

        return app;
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal, StoryService story, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await story.ListAsync(userId, DateTimeOffset.UtcNow, ct));
    }

    private static async Task<IResult> Read(
        string code, ClaimsPrincipal principal, StoryService story, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Results.Unauthorized();
        }

        var chapter = await story.ReadAsync(userId, code, DateTimeOffset.UtcNow, ct);

        // Chương chưa mở và chương không tồn tại trả về cùng một câu: phân biệt chúng
        // là cho phép dò trước có bao nhiêu chương và chúng tên gì.
        return chapter is null
            ? Results.NotFound(new
            {
                error = "chapter_not_available",
                message = "Chương này chưa mở. Học tiếp để mở nó ra.",
            })
            : Results.Ok(chapter);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
