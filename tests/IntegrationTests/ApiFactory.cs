using System.Net.Http.Json;
using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace EnglishForIT.IntegrationTests;

/// <summary>
/// Dựng nguyên cả API trên một Postgres thật trong container.
///
/// Vì sao không dùng DB trong bộ nhớ như test đơn vị: những thứ hay hỏng nhất ở tầng này
/// chỉ tồn tại với Postgres thật — migration, cột jsonb, enum lưu dạng chuỗi, query filter
/// xoá mềm, và ràng buộc khoá ngoại. DB trong bộ nhớ cho qua hết những cái đó rồi
/// production mới vỡ.
///
/// Container dựng một lần cho cả tập test (xem <see cref="ApiCollection"/>): khởi động API
/// còn chạy migration và seed 58 bài, dựng lại cho từng lớp test là phí vài phút mỗi lượt chạy.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("efit_test")
        .WithUsername("efit")
        .WithPassword("test-only-password")
        .Build();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        // Ép host dựng ngay để migration và seed chạy xong trước test đầu tiên.
        _ = Services.GetRequiredService<IHost>();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _db.GetConnectionString());

        // Nội dung thật, không phải bản rút gọn cho test: cả seeder lẫn cổng chất lượng
        // phải được kiểm trên đúng 58 file sẽ chạy ở production.
        builder.UseSetting("Content:Root", FindContentRoot());

        // TestServer chạy http nên cookie Secure sẽ không bao giờ tới được client.
        builder.UseSetting("Cookies:Secure", "false");

        // Mọi request từ TestServer không có IP nên rơi vào cùng một phân vùng hạn mức.
        // Giữ nguyên mức production thì test thứ mười một trở đi ăn 429.
        builder.UseSetting("RateLimits:auth:PermitLimit", "10000");
        builder.UseSetting("RateLimits:global:PermitLimit", "10000");
    }

    /// <summary>Một client riêng, cookie không dùng chung giữa các test.</summary>
    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
    });

    /// <summary>Đăng ký rồi đăng nhập, trả về client đã mang cookie phiên và cookie CSRF.</summary>
    public async Task<HttpClient> NewLearnerAsync(string? email = null)
    {
        var client = NewClient();
        var address = email ?? $"hv-{Guid.NewGuid():N}@vidu.vn";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = address,
            password = "mat-khau-du-dai-2026",
            displayName = "Học viên kiểm thử",
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = address,
            password = "mat-khau-du-dai-2026",
        });
        login.EnsureSuccessStatusCode();

        // Frontend đọc cookie CSRF rồi đặt vào header; ở đây làm đúng việc đó.
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ReadCsrfCookie(login));

        return client;
    }

    /// <summary>
    /// Như <see cref="NewLearnerAsync"/> nhưng cấp thêm vai trò Admin, rồi đăng nhập LẠI.
    ///
    /// Phải đăng nhập lại: vai trò nằm trong claim của phiên, nên phiên tạo trước lúc cấp
    /// quyền vẫn là phiên của học viên thường và mọi endpoint quản trị sẽ trả 403.
    /// </summary>
    public async Task<HttpClient> NewAdminAsync()
    {
        var email = $"qt-{Guid.NewGuid():N}@vidu.vn";
        await NewLearnerAsync(email);

        await using (var scope = NewScope())
        {
            var db = Db(scope);
            var user = await db.Users.FirstAsync(u => u.Email == email);

            db.UserRoles.Add(new UserRoleAssignment { UserId = user.Id, Role = UserRole.Admin });
            await db.SaveChangesAsync();
        }

        var client = NewClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "mat-khau-du-dai-2026",
        });
        login.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Add("X-CSRF-Token", ReadCsrfCookie(login));

        return client;
    }

    /// <summary>Mở AppDbContext để kiểm trực tiếp trong DB những gì API không trả ra.</summary>
    public AsyncServiceScope NewScope() => Services.CreateAsyncScope();

    public static AppDbContext Db(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    private static string ReadCsrfCookie(HttpResponseMessage response)
    {
        var cookie = response.Headers
            .GetValues("Set-Cookie")
            .First(c => c.StartsWith("efit_csrf=", StringComparison.Ordinal));

        return cookie.Split(';')[0]["efit_csrf=".Length..];
    }

    /// <summary>
    /// Tìm thư mục content bằng cách đi ngược lên từ chỗ đặt file test.
    /// Không hardcode đường dẫn tương đối vì nó đổi theo cấu hình build.
    /// </summary>
    private static string FindContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "content");

            if (Directory.Exists(Path.Combine(candidate, "lessons")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Không tìm thấy thư mục content khi đi ngược từ " + AppContext.BaseDirectory);
    }
}

/// <summary>Gom mọi lớp test vào một tập để dùng chung một container và một lần seed.</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
