using EnglishForIT.Application.Abstractions;
using EnglishForIT.Application.Identity;
using EnglishForIT.Application.Content;
using EnglishForIT.Application.Learning;
using EnglishForIT.Infrastructure.Ai;
using EnglishForIT.Infrastructure.Content;
using EnglishForIT.Infrastructure.Learning;
using EnglishForIT.Infrastructure.Ops;
using EnglishForIT.Infrastructure.Persistence;
using EnglishForIT.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishForIT.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Đăng ký toàn bộ tầng hạ tầng. API và Worker gọi chung hàm này để hai tiến trình
    /// không bao giờ lệch cấu hình.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Thiếu ConnectionStrings__Postgres. Xem .env.example.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                // Chỉ thử lại lỗi tạm thời. Số lần thấp vì API phải trả lời nhanh,
                // để worker gánh phần chờ lâu.
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });

            // Truy vấn nào cần theo dõi thay đổi thì bật tại chỗ. Mặc định không theo dõi
            // cho các truy vấn đọc, vốn chiếm phần lớn tải.
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        });

        services.AddScoped<IAuthDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddOptions<PasswordHashingOptions>()
            .Bind(config.GetSection("PasswordHashing"))
            .ValidateOnStart();

        services.AddOptions<SecretProtectionOptions>()
            .Bind(config.GetSection("SecretProtection"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.MasterKey) && o.MasterKey.Length >= 32,
                "SecretProtection__MasterKey phải có ít nhất 32 ký tự.")
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(config.GetSection("Auth"))
            .ValidateOnStart();

        services.AddOptions<LearningPolicyOptions>()
            .Bind(config.GetSection(LearningPolicyOptions.SectionName))
            .Validate(o => o.PerSkillThreshold <= o.MasteryThreshold,
                "PerSkillThreshold khong duoc cao hon MasteryThreshold.")
            .Validate(o => o.ChallengePassThreshold >= o.MasteryThreshold,
                "ChallengePassThreshold phai cao hon hoac bang MasteryThreshold.")
            .Validate(o => o.SpeechAudioRetentionDays is >= 1 and <= 365,
                "SpeechAudioRetentionDays phai nam trong khoang 1 den 365 ngay.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IAuthService, AuthService>();

        // Nội dung
        services.AddSingleton<LessonValidator>();
        services.AddScoped<YamlContentLoader>();
        services.AddScoped<ContentSeeder>();
        services.AddSingleton<PlacementValidator>();
        services.AddScoped<PlacementSeeder>();

        // Lộ trình học và màn học
        services.AddScoped<LearningPathService>();
        services.AddScoped<LessonPlayerService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<PlacementService>();
        services.AddScoped<ChallengeService>();
        services.AddScoped<ConsolidationService>();
        services.AddScoped<StreakService>();
        services.AddScoped<RoleplayService>();
        services.AddScoped<SpeechService>();
        services.AddScoped<StoryService>();
        services.AddScoped<WritingService>();

        // AI: gateway là cửa vào duy nhất, các client chỉ biết hình dạng API của hãng mình.
        services.AddHttpClient();
        services.AddScoped<AiGateway>();
        services.AddScoped<IAiProviderClient, AnthropicClient>();
        services.AddScoped<IAiProviderClient, OpenAiClient>();
        services.AddScoped<IAiProviderClient, GeminiClient>();
        services.AddScoped<IAiProviderClient, OpenRouterClient>();
        services.AddScoped<IAiProviderClient, OllamaClient>();
        services.AddScoped<IAiProviderClient, AzureOpenAiClient>();
        services.AddScoped<RoleplaySeeder>();
        services.AddScoped<TtsManifestWriter>();
        services.AddSingleton<RoleplayValidator>();
        services.AddScoped<StorySeeder>();
        services.AddSingleton<StoryValidator>();
        services.AddScoped<WritingSeeder>();
        services.AddSingleton<WritingValidator>();

        // Vận hành: thông báo, hộp thư đi, và các job định kỳ của worker.
        services.AddScoped<NotificationService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<OutboxProcessor>();
        services.AddScoped<LearnerReminderJob>();
        services.AddScoped<AudioRetentionJob>();

        return services;
    }
}
