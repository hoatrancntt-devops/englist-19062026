using System.Security.Claims;

namespace EnglishForIT.Api.Modules;

/// <summary>
/// Kiểm quyền quản trị.
///
/// <b>Không dùng <c>Results.Forbid()</c>.</b> Hàm đó yêu cầu có scheme xác thực đã đăng ký để
/// xử lý challenge; app này xác thực bằng cookie phiên tự quản chứ không qua middleware
/// authentication, nên <c>Forbid()</c> ném lỗi và người gọi nhận 500 thay vì 403 — nghĩa là
/// một lỗi phân quyền trông y hệt một lỗi máy chủ.
/// </summary>
public static class AdminAccess
{
    public static bool IsAdmin(ClaimsPrincipal principal) =>
        principal.FindAll(ClaimTypes.Role).Any(r => r.Value is "Admin" or "SuperAdmin");

    public static Guid? UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>Trả 403 với đúng hình dạng lỗi mà phần còn lại của API dùng.</summary>
    public static IResult Denied() =>
        Results.Json(
            new { error = "forbidden", message = "Bạn không có quyền mở phần này." },
            statusCode: StatusCodes.Status403Forbidden);
}
