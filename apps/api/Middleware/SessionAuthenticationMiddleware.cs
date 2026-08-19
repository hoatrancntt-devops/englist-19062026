using System.Security.Claims;
using EnglishForIT.Application.Identity;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.Api.Middleware;

/// <summary>
/// Đọc cookie phiên, tra DB, dựng ClaimsPrincipal.
///
/// Vì sao tự viết thay vì dùng cookie authentication có sẵn: phiên của hệ thống này lưu ở DB
/// và phải thu hồi được tức thì. Handler mặc định giữ trạng thái trong cookie đã mã hoá,
/// nên đăng xuất trên một thiết bị không tác động tới thiết bị khác.
/// </summary>
public class SessionAuthenticationMiddleware(RequestDelegate next)
{
    public const string CookieName = "efit_session";
    public const string CsrfCookieName = "efit_csrf";

    public async Task InvokeAsync(HttpContext context, IAuthService auth, AppDbContext db)
    {
        var token = context.Request.Cookies[CookieName];

        if (!string.IsNullOrWhiteSpace(token))
        {
            var session = await auth.ResolveSessionAsync(token, context.RequestAborted);

            if (session?.User is not null)
            {
                var roles = await db.UserRoles
                    .Where(r => r.UserId == session.UserId)
                    .Select(r => r.Role)
                    .ToListAsync(context.RequestAborted);

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                    new(ClaimTypes.Email, session.User.Email),
                    new(ClaimTypes.Name, session.User.DisplayName),
                    new("session_id", session.Id.ToString())
                };

                claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.ToString())));

                // Learner là vai mặc định: tài khoản cũ thiếu bản ghi vai trò vẫn dùng được app.
                if (roles.Count == 0)
                {
                    claims.Add(new Claim(ClaimTypes.Role, UserRole.Learner.ToString()));
                }

                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "session"));
                context.Items["session_csrf"] = session.CsrfSecret;
            }
        }

        await next(context);
    }
}
