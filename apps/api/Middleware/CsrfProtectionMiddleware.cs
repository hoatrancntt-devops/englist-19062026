namespace EnglishForIT.Api.Middleware;

/// <summary>
/// Chống CSRF bằng double-submit: cookie phiên là HttpOnly nên JavaScript không đọc được,
/// còn bí mật CSRF nằm ở một cookie đọc được và phải được gửi lại qua header.
/// Trang của kẻ tấn công gửi kèm được cookie nhưng không đọc được để đặt header.
///
/// Chỉ kiểm với phương thức làm thay đổi dữ liệu. GET và HEAD bỏ qua.
/// </summary>
public class CsrfProtectionMiddleware(RequestDelegate next, ILogger<CsrfProtectionMiddleware> logger)
{
    public const string HeaderName = "X-CSRF-Token";

    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task InvokeAsync(HttpContext context)
    {
        if (SafeMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        // Chưa đăng nhập thì không có gì để giả mạo: đăng nhập và đăng ký tự bảo vệ
        // bằng rate limit chứ không bằng CSRF token.
        if (context.Items["session_csrf"] is not string sessionSecret)
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(provided) || !FixedTimeEquals(provided, sessionSecret))
        {
            logger.LogWarning("Từ chối request thiếu hoặc sai CSRF token: {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "csrf_failed",
                message = "Phiên làm việc không hợp lệ. Tải lại trang rồi thử lại."
            });
            return;
        }

        await next(context);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }
}
