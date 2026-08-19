using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.Api.Modules;

/// <summary>
/// Ba endpoint sức khoẻ với ba mục đích khác nhau — trộn chúng làm một là nguyên nhân
/// kinh điển khiến container bị giết oan lúc khởi động.
/// </summary>
public static class HealthModule
{
    /// <summary>Đặt true khi migration đã chạy xong và app sẵn sàng nhận tải.</summary>
    public static volatile bool StartupCompleted;

    public static IEndpointRouteBuilder MapHealthModule(this IEndpointRouteBuilder app)
    {
        // Tiến trình còn sống. Không chạm DB: DB chết không phải lý do để khởi động lại app.
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
            .WithTags("Health")
            .ExcludeFromDescription();

        // Sẵn sàng nhận request. Có chạm DB: mất DB thì rút khỏi vòng cân bằng tải.
        app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
        {
            if (!StartupCompleted)
            {
                return Results.Json(new { status = "starting" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
                return Results.Ok(new { status = "ready" });
            }
            catch (Exception)
            {
                // Không trả chi tiết lỗi ra ngoài: endpoint này không cần xác thực.
                return Results.Json(new { status = "degraded", reason = "database_unreachable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithTags("Health");

        // Đã qua giai đoạn khởi động chậm (migration). Docker dùng cái này làm start_period.
        app.MapGet("/health/startup", () => StartupCompleted
                ? Results.Ok(new { status = "started" })
                : Results.Json(new { status = "starting" }, statusCode: StatusCodes.Status503ServiceUnavailable))
            .WithTags("Health");

        return app;
    }
}
