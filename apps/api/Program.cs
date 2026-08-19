using System.Threading.RateLimiting;
using EnglishForIT.Api.Middleware;
using EnglishForIT.Api.Modules;
using EnglishForIT.Application.Identity;
using EnglishForIT.Infrastructure;
using EnglishForIT.Infrastructure.Content;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Log JSON gọn ra stdout. Docker gom stdout, nên không cần sink file và không cần xoay vòng log.
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Chạy sau Caddy nên IP thật nằm ở X-Forwarded-For. Không có dòng này thì rate limit
// sẽ tính chung toàn bộ người dùng vào một IP của proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Hạn mức đọc từ cấu hình, mặc định giữ nguyên giá trị cũ. Hardcode thì vừa không tinh
// chỉnh được lúc vận hành, vừa làm chính các endpoint xác thực không test tự động nổi:
// test tích hợp chạy trên máy chủ trong bộ nhớ nên mọi request rơi vào cùng một phân vùng.
var rateLimits = builder.Configuration.GetSection("RateLimits");

void AddFixedWindow(RateLimiterOptions options, string name, int permits, int windowSeconds, int queue)
{
    var section = rateLimits.GetSection(name);

    options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: PartitionKey(context),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = section.GetValue("PermitLimit", permits),
            Window = TimeSpan.FromSeconds(section.GetValue("WindowSeconds", windowSeconds)),
            QueueLimit = section.GetValue("QueueLimit", queue)
        }));
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Chặt tay với các endpoint xác thực: đây là nơi bị dò mật khẩu.
    AddFixedWindow(options, "auth", permits: 10, windowSeconds: 300, queue: 0);

    // Ghi âm tốn CPU của dịch vụ nhận dạng giọng nói, nên giới hạn riêng.
    AddFixedWindow(options, "speech", permits: 30, windowSeconds: 60, queue: 5);

    var global = rateLimits.GetSection("global");

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: PartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = global.GetValue("PermitLimit", 300),
                Window = TimeSpan.FromSeconds(global.GetValue("WindowSeconds", 60)),
                QueueLimit = global.GetValue("QueueLimit", 0)
            }));
});

// Chỉ dùng lúc dev: production phục vụ web và api cùng gốc qua Caddy nên không cần CORS.
var devOrigins = builder.Configuration.GetSection("Cors:DevOrigins").Get<string[]>() ?? [];
if (devOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy("dev", policy => policy
        .WithOrigins(devOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
}

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "{RequestMethod} {RequestPath} => {StatusCode} trong {Elapsed:0} ms";
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    if (devOrigins.Length > 0)
    {
        app.UseCors("dev");
    }
}

app.UseRateLimiter();

// Thứ tự bắt buộc: dựng danh tính trước, rồi mới kiểm CSRF — vì kiểm CSRF cần bí mật của phiên.
app.UseMiddleware<SessionAuthenticationMiddleware>();
app.UseMiddleware<CsrfProtectionMiddleware>();

app.MapHealthModule();
app.MapAuthModule();
app.MapLearningModule();
app.MapPlacementModule();
app.MapRoleplayModule();
app.MapAiModule();
app.MapAdminModule();
app.MapAdminContentModule();
app.MapSpeechModule();
app.MapMedia();

// Migration chạy lúc khởi động thay vì bằng lệnh riêng: một container, một lệnh, không quên bước.
// Đổi lại: chỉ được chạy một bản sao API lúc nâng cấp. Ghi rõ trong AZURE_VM_DEPLOYMENT.md.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    {
        logger.LogInformation("Đang chạy migration...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Migration xong");
    }

    // Nạp nội dung từ YAML. Upsert theo mã bài nên chạy lại bao nhiêu lần cũng
    // không đụng tới tiến độ học viên.
    if (app.Configuration.GetValue("Content:SeedOnStartup", true))
    {
        var contentRoot = app.Configuration.GetValue<string>("Content:Root") ?? "content";
        var audioRoot = app.Configuration.GetValue<string>("Storage:AudioRoot") ?? "media";
        var seeder = scope.ServiceProvider.GetRequiredService<ContentSeeder>();

        var report = await seeder.SeedAsync(contentRoot);

        foreach (var problem in report.Problems)
        {
            logger.LogError("Nội dung có vấn đề: {Problem}", problem);
        }

        // Nội dung hỏng KHÔNG làm sập app: học viên vẫn đăng nhập và xem được bài cũ.
        // Nhưng lỗi phải nằm chình ình trong log để không ai bỏ qua.
        if (report.HasProblems)
        {
            logger.LogError("Seed nội dung thất bại với {Count} vấn đề. Nội dung cũ trong DB giữ nguyên.",
                report.Problems.Count);
        }

        // Đề xếp lớp nạp riêng: đề hỏng không được ngăn bài học vào DB, và ngược lại.
        //
        // Bọc try/catch vì cùng một lý do làm cho lỗi nội dung chỉ ghi log ở trên:
        // học viên vẫn phải đăng nhập và học được bài cũ khi đề xếp lớp có vấn đề.
        // Không bọc thì một ngoại lệ ở đây làm sập cả API lúc khởi động.
        try
        {
            var placementSeeder = scope.ServiceProvider.GetRequiredService<PlacementSeeder>();
            var placementReport = await placementSeeder.SeedAsync(contentRoot);

            foreach (var problem in placementReport.Problems)
            {
                logger.LogError("Đề xếp lớp có vấn đề: {Problem}", problem);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seed đề xếp lớp thất bại. Đề cũ trong DB giữ nguyên, app vẫn chạy.");
        }

        // Kịch bản roleplay nạp riêng, cùng lý do: một loại nội dung hỏng không được
        // ngăn các loại còn lại vào DB.
        try
        {
            var roleplaySeeder = scope.ServiceProvider.GetRequiredService<RoleplaySeeder>();
            var roleplayReport = await roleplaySeeder.SeedAsync(contentRoot);

            foreach (var problem in roleplayReport.Problems)
            {
                logger.LogError("Kịch bản roleplay có vấn đề: {Problem}", problem);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seed roleplay thất bại. Kịch bản cũ trong DB giữ nguyên, app vẫn chạy.");
        }

        // Danh sách đoạn cần đọc thành tiếng, cho bước sinh giọng chạy ngoài API.
        //
        // Ghi mỗi lần khởi động vì nội dung thay đổi theo từng lần triển khai. Không sinh audio
        // ở đây: nạp model Piper và đọc cả nghìn câu là việc của một mẻ chạy riêng, chặn khởi
        // động API để làm việc đó là bắt học viên chờ vài phút mới đăng nhập được.
        try
        {
            var manifestWriter = scope.ServiceProvider.GetRequiredService<TtsManifestWriter>();
            await manifestWriter.WriteAsync(contentRoot, audioRoot);
        }
        catch (Exception ex)
        {
            // Cùng lý do với các seeder trên: không ghi được danh sách thì phần nghe lùi về
            // giọng của trình duyệt, chứ không được làm sập đường đăng nhập.
            logger.LogError(ex, "Ghi danh sách đoạn cần đọc thất bại. Phần nghe dùng giọng trình duyệt.");
        }
    }

    // Tài khoản quản trị đầu tiên.
    //
    // AuthService.EnsureAdminAsync có từ đầu nhưng chưa nơi nào gọi, nên ADMIN_EMAIL và
    // ADMIN_PASSWORD trong .env.example không có tác dụng gì: triển khai xong là không ai vào
    // được trang quản trị, và cách duy nhất còn lại là sửa thẳng bảng user_roles bằng SQL.
    //
    // Chạy lại nhiều lần không sao: đã có tài khoản thì hàm chỉ bảo đảm vai trò, KHÔNG đặt lại
    // mật khẩu — đổi mật khẩu trên web xong mà lần khởi động sau bị ghi đè về giá trị trong
    // .env thì còn tệ hơn là không có bước này.
    var adminEmail = app.Configuration["ADMIN_EMAIL"];
    var adminPassword = app.Configuration["ADMIN_PASSWORD"];

    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        try
        {
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await auth.EnsureAdminAsync(adminEmail, adminPassword, "Quản trị");
        }
        catch (Exception ex)
        {
            // Bọc vì cùng lý do với các seeder trên: mật khẩu quá ngắn hoặc email sai định dạng
            // không được làm sập API và cắt luôn đường vào của học viên.
            logger.LogError(ex, "Tạo tài khoản quản trị thất bại. App vẫn chạy.");
        }
    }
    else
    {
        // Mức thông tin chứ không phải cảnh báo: dev và test cố ý không đặt hai biến này, và
        // một cảnh báo lặp lại mỗi lần khởi động chỉ dạy người đọc log bỏ qua cảnh báo.
        logger.LogInformation("Chua dat ADMIN_EMAIL/ADMIN_PASSWORD nen bo qua buoc tao tai khoan quan tri.");
    }
}

HealthModule.StartupCompleted = true;

app.Run();

static string PartitionKey(HttpContext context)
{
    // Đã đăng nhập thì tính theo user: nhiều người sau cùng một NAT văn phòng
    // không được làm nhau bị chặn.
    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    return userId ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

/// <summary>Lộ ra để dự án test tích hợp dựng được host bằng WebApplicationFactory.</summary>
public partial class Program;