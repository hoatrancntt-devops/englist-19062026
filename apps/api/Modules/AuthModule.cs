using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using EnglishForIT.Api.Middleware;
using EnglishForIT.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EnglishForIT.Api.Modules;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MinLength(10), MaxLength(256)] string Password,
    [Required, MaxLength(120)] string DisplayName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(10), MaxLength(256)] string NewPassword);

public record MeResponse(Guid Id, string Email, string DisplayName, string[] Roles);

public static class AuthModule
{
    public static IEndpointRouteBuilder MapAuthModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .RequireRateLimiting("auth")
            .WithSummary("Đăng ký tài khoản học viên");

        group.MapPost("/login", Login)
            .RequireRateLimiting("auth")
            .WithSummary("Đăng nhập, đặt cookie phiên và cookie CSRF");

        group.MapPost("/logout", Logout)
            .WithSummary("Thu hồi phiên hiện tại");

        group.MapGet("/me", Me)
            .WithSummary("Thông tin tài khoản đang đăng nhập");

        group.MapPost("/change-password", ChangePassword)
            .RequireRateLimiting("auth")
            .WithSummary("Đổi mật khẩu, vô hiệu hoá mọi phiên cũ");

        return app;
    }

    private static async Task<IResult> Register(
        [FromBody] RegisterRequest request,
        IAuthService auth,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            await auth.RegisterAsync(request.Email, request.Password, request.DisplayName, ct);
        }
        catch (InvalidOperationException)
        {
            // Email đã tồn tại. KHÔNG nói ra: trang đăng ký sẽ thành công cụ dò xem
            // ai đã có tài khoản. Trả về đúng phản hồi như trường hợp thành công.
            logger.LogInformation("Đăng ký với email đã tồn tại, trả về phản hồi trung tính");
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = "invalid_password", message = ex.Message });
        }

        return Results.Ok(new
        {
            message = "Nếu email hợp lệ, tài khoản đã được tạo. Đăng nhập để tiếp tục."
        });
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        IAuthService auth,
        HttpContext http,
        IConfiguration config,
        CancellationToken ct)
    {
        var ip = http.Connection.RemoteIpAddress?.ToString();
        var userAgent = http.Request.Headers.UserAgent.ToString();

        var result = await auth.AuthenticateAsync(request.Email, request.Password, ip, userAgent, ct);

        if (!result.Success)
        {
            // Một thông báo duy nhất cho mọi nguyên nhân: email lạ, sai mật khẩu, đang bị khoá.
            return Results.Json(
                new { error = "invalid_credentials", message = "Email hoặc mật khẩu không đúng." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var secure = config.GetValue("Cookies:Secure", true);

        http.Response.Cookies.Append(SessionAuthenticationMiddleware.CookieName, result.SessionToken!,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(30)
            });

        // Cookie CSRF cố ý KHÔNG HttpOnly: frontend phải đọc được để đặt vào header.
        http.Response.Cookies.Append(SessionAuthenticationMiddleware.CsrfCookieName, result.CsrfToken!,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(30)
            });

        return Results.Ok(new { userId = result.UserId });
    }

    private static async Task<IResult> Logout(IAuthService auth, HttpContext http, CancellationToken ct)
    {
        var token = http.Request.Cookies[SessionAuthenticationMiddleware.CookieName];

        if (!string.IsNullOrWhiteSpace(token))
        {
            await auth.RevokeSessionAsync(token, ct);
        }

        http.Response.Cookies.Delete(SessionAuthenticationMiddleware.CookieName);
        http.Response.Cookies.Delete(SessionAuthenticationMiddleware.CsrfCookieName);

        return Results.Ok(new { message = "Đã đăng xuất." });
    }

    private static IResult Me(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

        return Results.Ok(new MeResponse(
            id,
            user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            user.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            roles));
    }

    private static async Task<IResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        ClaimsPrincipal user,
        IAuthService auth,
        HttpContext http,
        CancellationToken ct)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var ok = await auth.ChangePasswordAsync(id, request.CurrentPassword, request.NewPassword, ct);

            if (!ok)
            {
                return Results.BadRequest(new
                {
                    error = "invalid_current_password",
                    message = "Mật khẩu hiện tại không đúng."
                });
            }
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = "invalid_password", message = ex.Message });
        }

        // Đổi mật khẩu vô hiệu mọi phiên, kể cả phiên đang gọi. Xoá cookie để client
        // không phải đoán vì sao request tiếp theo bị 401.
        http.Response.Cookies.Delete(SessionAuthenticationMiddleware.CookieName);
        http.Response.Cookies.Delete(SessionAuthenticationMiddleware.CsrfCookieName);

        return Results.Ok(new { message = "Đã đổi mật khẩu. Đăng nhập lại trên mọi thiết bị." });
    }
}
