using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Application.Identity;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EnglishForIT.IntegrationTests;

/// <summary>
/// Xác thực.
///
/// Trọng tâm là các bất biến bảo mật, không phải đường hạnh phúc: chống dò tài khoản,
/// cờ trên cookie, và CSRF. Ba thứ đó trước đây chỉ kiểm bằng tay qua curl, nghĩa là
/// không có gì ngăn lần sửa sau làm hỏng chúng trong im lặng.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthEndpointsTests(ApiFactory api)
{
    [Fact]
    public async Task SeedTaiKhoanQuanTriTaoDuocVaKhongGhiDeMatKhauDangDung()
    {
        await using var scope = api.NewScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var db = ApiFactory.Db(scope);

        var email = $"quantri-{Guid.NewGuid():N}@vidu.vn";

        await auth.EnsureAdminAsync(email, "mat-khau-ban-dau-2026", "Quản trị");

        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.True(await db.UserRoles.AnyAsync(r => r.UserId == user.Id && r.Role == UserRole.SuperAdmin));

        // Quản trị viên đổi mật khẩu trên web.
        Assert.True(await auth.ChangePasswordAsync(user.Id, "mat-khau-ban-dau-2026", "mat-khau-da-doi-2026"));

        // Rồi container khởi động lại, và seed chạy lần nữa với giá trị CŨ còn nằm trong .env.
        await auth.EnsureAdminAsync(email, "mat-khau-ban-dau-2026", "Quản trị");

        // Mật khẩu mới phải còn nguyên. Ghi đè ở đây nghĩa là mỗi lần khởi động lại, mật khẩu
        // quản trị âm thầm quay về giá trị trong .env — và người đổi nó tưởng mình đã đổi rồi.
        Assert.True((await auth.AuthenticateAsync(email, "mat-khau-da-doi-2026", null, null)).Success);
        Assert.False((await auth.AuthenticateAsync(email, "mat-khau-ban-dau-2026", null, null)).Success);
    }

    [Fact]
    public async Task DangKyTrungEmailTraVeYHetLanDau()
    {
        var client = api.NewClient();
        var email = $"trung-{Guid.NewGuid():N}@vidu.vn";

        var body = new { email, password = "mat-khau-du-dai-2026", displayName = "Người thứ nhất" };

        var first = await client.PostAsJsonAsync("/api/v1/auth/register", body);
        var second = await client.PostAsJsonAsync("/api/v1/auth/register", body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Khác một ký tự thôi là trang đăng ký thành công cụ dò xem ai đã có tài khoản.
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EmailLaVaSaiMatKhauTraVeGiongHetNhau()
    {
        var client = api.NewClient();
        var email = $"co-that-{Guid.NewGuid():N}@vidu.vn";

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "mat-khau-du-dai-2026",
            displayName = "Người dùng",
        });

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "mat-khau-sai-hoan-toan",
        });

        var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = $"khong-ton-tai-{Guid.NewGuid():N}@vidu.vn",
            password = "mat-khau-sai-hoan-toan",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownEmail.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DangNhapDatHaiCookieVoiCoDungNhu_thietKe()
    {
        var client = api.NewClient();
        var email = $"cookie-{Guid.NewGuid():N}@vidu.vn";

        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "mat-khau-du-dai-2026",
            displayName = "Người dùng",
        });

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "mat-khau-du-dai-2026",
        });

        var cookies = login.Headers.GetValues("Set-Cookie").ToList();

        var session = cookies.Single(c => c.StartsWith("efit_session=", StringComparison.Ordinal));
        var csrf = cookies.Single(c => c.StartsWith("efit_csrf=", StringComparison.Ordinal));

        // Cookie phiên phải HttpOnly, nếu không XSS đọc được nó.
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);

        // Cookie CSRF cố ý KHÔNG HttpOnly: frontend phải đọc được để đặt vào header.
        Assert.DoesNotContain("httponly", csrf, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ThieuHeaderCsrfThiBiChan()
    {
        var client = await api.NewLearnerAsync();

        client.DefaultRequestHeaders.Remove("X-CSRF-Token");

        var response = await client.PostAsJsonAsync("/api/v1/placement/start", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChuaDangNhapThiKhongVaoDuocDuLieuHocTap()
    {
        var client = api.NewClient();

        var response = await client.GetAsync("/api/v1/learning/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeTraVeDungDanhTinhVaVaiTroLearner()
    {
        var email = $"me-{Guid.NewGuid():N}@vidu.vn";
        var client = await api.NewLearnerAsync(email);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");

        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Contains("Learner", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task DangXuatXongThiPhienKhongDungLaiDuoc()
    {
        var client = await api.NewLearnerAsync();

        (await client.PostAsync("/api/v1/auth/logout", null)).EnsureSuccessStatusCode();

        var me = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }
}
