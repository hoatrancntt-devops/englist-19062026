using System.Security.Claims;
using EnglishForIT.Application.Ai;
using EnglishForIT.Infrastructure.Ai;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.Api.Modules;

public record AiTestRequest(string Prompt);

public record AiProviderStatus(string Provider, bool Enabled, bool HasKey, string? BaseUrl);

public record AiStatusResponse(
    string BudgetMode,
    decimal SpentThisMonthUsd,
    decimal MonthlyCapUsd,
    IReadOnlyList<AiProviderStatus> Providers,
    int CacheEntries,
    int CallsThisMonth,
    int CacheHitsThisMonth);

/// <summary>
/// Trạng thái và kiểm thử AI, dành cho quản trị viên.
///
/// Tồn tại vì cùng lý do với nút gửi thư thử: cấu hình khoá API rồi không biết nó đúng hay
/// sai cho tới khi học viên gặp lỗi là cách vận hành tệ nhất.
///
/// Khoá API không bao giờ trả ra, kể cả dạng che một phần — chỉ trả cờ có hay không.
/// </summary>
public static class AiModule
{
    public static IEndpointRouteBuilder MapAiModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/ai").WithTags("Admin AI");

        group.MapGet("/status", Status)
            .WithSummary("Chế độ ngân sách, chi phí tháng này, và nhà cung cấp đã cấu hình");

        group.MapPost("/test", Test)
            .WithSummary("Gọi thử một prompt ngắn qua gateway để xác nhận cấu hình chạy được");

        return app;
    }

    private static async Task<IResult> Status(
        ClaimsPrincipal principal, AppDbContext db, AiGateway gateway, IConfiguration config, CancellationToken ct)
    {
        if (!AdminAccess.IsAdmin(principal))
        {
            return AdminAccess.Denied();
        }

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var providers = await db.AiProviderSettings
            .AsNoTracking()
            .OrderBy(p => p.Provider)
            .Select(p => new AiProviderStatus(
                p.Provider.ToString(), p.Enabled, p.ApiKeyEncrypted != null, p.BaseUrl))
            .ToListAsync(ct);

        var usages = await db.AiUsages.Where(u => u.CreatedAt >= monthStart).ToListAsync(ct);

        return Results.Ok(new AiStatusResponse(
            (await gateway.CurrentModeAsync(now, ct)).ToString(),
            await gateway.SpentThisMonthAsync(now, ct),
            config.GetValue("Ai:MonthlyCapUsd", 50m),
            providers,
            await db.AiCacheEntries.CountAsync(ct),
            usages.Count,
            usages.Count(u => u.CacheHit)));
    }

    private static async Task<IResult> Test(
        [FromBody] AiTestRequest request, ClaimsPrincipal principal, AiGateway gateway, CancellationToken ct)
    {
        if (!AdminAccess.IsAdmin(principal))
        {
            return AdminAccess.Denied();
        }

        var response = await gateway.CompleteAsync(
            new AiRequest(
                TaskName: "admin.test",
                PromptVersion: "1",
                Tier: AiTier.T1,
                SystemPrompt: "You are a terse assistant. Answer in one short sentence.",
                UserPrompt: request.Prompt,
                CacheFor: TimeSpan.FromMinutes(5),
                MaxOutputTokens: 100),
            fallback: "Chưa gọi được AI. Đây là câu trả lời dự phòng do luật sinh.",
            DateTimeOffset.UtcNow,
            ct);

        return Results.Ok(response);
    }

    private static bool IsAdmin(ClaimsPrincipal principal) =>
        principal.FindAll(ClaimTypes.Role).Any(r => r.Value is "Admin" or "SuperAdmin");
}
