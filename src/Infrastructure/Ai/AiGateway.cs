using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnglishForIT.Application.Ai;
using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Ai;

/// <summary>Một nhà cung cấp AI cụ thể. Gateway không biết gì về hình dạng API của từng hãng.</summary>
public interface IAiProviderClient
{
    AiProvider Provider { get; }

    Task<AiProviderResult> CompleteAsync(
        AiProviderSetting setting, AiRequest request, AiTier tier, CancellationToken ct);
}

public record AiProviderResult(
    bool Success,
    string? Text,
    string Model,
    int InputTokens,
    int OutputTokens,
    string? ErrorCode);

/// <summary>
/// Cửa vào duy nhất để gọi AI.
///
/// Thứ tự cố định và không đổi: cache trước, rồi ngân sách, rồi nhà cung cấp, cuối cùng là
/// fallback bằng luật. Nguyên tắc bao trùm là <b>app phải chạy được khi không có AI</b> —
/// AI làm câu chữ hay hơn chứ không phải điều kiện để học viên học được.
///
/// Ba lý do rơi về fallback, cả ba đều bình thường chứ không phải sự cố: chưa cấu hình
/// nhà cung cấp nào, chạm trần ngân sách tháng, hoặc mọi nhà cung cấp đều lỗi.
/// </summary>
public class AiGateway(
    AppDbContext db,
    IEnumerable<IAiProviderClient> clients,
    IConfiguration configuration,
    ILogger<AiGateway> logger)
{
    private readonly decimal _monthlyCapUsd = configuration.GetValue("Ai:MonthlyCapUsd", 50m);

    /// <summary>
    /// Gọi AI, hoặc trả về câu fallback.
    /// </summary>
    /// <param name="fallback">
    /// Câu trả lời khi không gọi được AI. BẮT BUỘC truyền — không có tham số mặc định,
    /// vì mọi chỗ gọi đều phải nghĩ trước xem thiếu AI thì hiện gì cho học viên.
    /// </param>
    public async Task<AiResponse> CompleteAsync(
        AiRequest request, string fallback, DateTimeOffset now, CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(request);

        var cached = await db.AiCacheEntries
            .FirstOrDefaultAsync(e => e.CacheKey == cacheKey && e.ExpiresAt > now, ct);

        if (cached is not null)
        {
            cached.HitCount++;
            cached.LastHitAt = now;

            await LogUsageAsync(request, cached.Provider, cached.Model, 0, 0, 0m, true, true, null, 0, ct);
            await db.SaveChangesAsync(ct);

            return new AiResponse(ReadCachedText(cached.ResponseJson), true, cached.Provider, cached.Model, false);
        }

        var spent = await SpentThisMonthAsync(now, ct);
        var mode = AiBudgetPolicy.ModeFor(spent, _monthlyCapUsd);

        if (!AiBudgetPolicy.CanCallProvider(mode))
        {
            logger.LogWarning(
                "Ngân sách AI đã dùng {Spent}/{Cap} USD, chỉ còn cache. Tác vụ {Task} dùng fallback.",
                spent, _monthlyCapUsd, request.TaskName);

            return Fallback(fallback, "budget_cache_only");
        }

        var settings = await db.AiProviderSettings
            .AsNoTracking()
            .Where(s => s.Enabled && s.ApiKeyEncrypted != null)
            .ToListAsync(ct);

        if (settings.Count == 0)
        {
            // Chưa cấu hình nhà cung cấp nào là trạng thái hợp lệ, không phải lỗi:
            // toàn bộ phần học vẫn chạy bằng luật.
            return Fallback(fallback, "no_provider_configured");
        }

        var tier = AiBudgetPolicy.EffectiveTier(request.Tier, mode);

        foreach (var setting in settings)
        {
            var client = clients.FirstOrDefault(c => c.Provider == setting.Provider);

            if (client is null)
            {
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            AiProviderResult result;

            try
            {
                result = await client.CompleteAsync(setting, request, tier, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Một nhà cung cấp hỏng thì thử nhà kế tiếp. KHÔNG ghi nội dung lỗi vào
                // usage log: thông điệp của nhà cung cấp đôi khi kèm lại cả prompt.
                logger.LogWarning(ex, "Nhà cung cấp {Provider} lỗi, thử nhà kế tiếp", setting.Provider);
                result = new AiProviderResult(false, null, "", 0, 0, "provider_exception");
            }

            stopwatch.Stop();

            var cost = EstimateCost(setting.Provider, result.InputTokens, result.OutputTokens);

            await LogUsageAsync(
                request, setting.Provider, result.Model, result.InputTokens, result.OutputTokens,
                cost, false, result.Success, result.ErrorCode, (int)stopwatch.ElapsedMilliseconds, ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            {
                continue;
            }

            db.AiCacheEntries.Add(new AiCacheEntry
            {
                CacheKey = cacheKey,
                TaskName = request.TaskName,
                PromptVersion = request.PromptVersion,
                // Cột là jsonb nên phải là JSON hợp lệ. Câu trả lời của model là chữ thô,
                // ghi thẳng vào sẽ bị Postgres từ chối với "invalid input syntax for type json".
                ResponseJson = JsonSerializer.Serialize(result.Text),
                Provider = setting.Provider,
                Model = result.Model,
                ExpiresAt = now.Add(AiBudgetPolicy.EffectiveCacheDuration(request.CacheFor, mode)),
            });

            await db.SaveChangesAsync(ct);

            return new AiResponse(result.Text, false, setting.Provider, result.Model, false);
        }

        await db.SaveChangesAsync(ct);

        return Fallback(fallback, "all_providers_failed");
    }

    /// <summary>Chi phí đã dùng trong tháng dương lịch hiện tại, tính theo UTC.</summary>
    public async Task<decimal> SpentThisMonthAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return await db.AiUsages
            .Where(u => u.CreatedAt >= monthStart)
            .SumAsync(u => u.EstimatedCostUsd, ct);
    }

    public async Task<AiBudgetMode> CurrentModeAsync(DateTimeOffset now, CancellationToken ct = default) =>
        AiBudgetPolicy.ModeFor(await SpentThisMonthAsync(now, ct), _monthlyCapUsd);

    /// <summary>Gỡ lớp bọc JSON của câu trả lời trong cache.</summary>
    private static string ReadCachedText(string responseJson)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(responseJson) ?? string.Empty;
        }
        catch (JsonException)
        {
            // Bản ghi cache cũ ghi chữ thô. Trả nguyên văn thay vì ném lỗi.
            return responseJson;
        }
    }

    private static AiResponse Fallback(string text, string reason) =>
        new(text, false, null, null, true, reason);

    /// <summary>
    /// Khoá cache gồm tác vụ, phiên bản prompt và nội dung prompt.
    ///
    /// Phiên bản prompt nằm trong khoá là điều bắt buộc: sửa prompt mà quên tăng số đó thì
    /// cache tiếp tục trả câu trả lời sinh từ prompt cũ, và không có gì báo.
    /// </summary>
    private static string BuildCacheKey(AiRequest request)
    {
        var raw = $"{request.TaskName}|{request.PromptVersion}|{request.SystemPrompt}|{request.UserPrompt}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Ước tính chi phí. Bảng giá thô theo bậc, đủ để quyết định hạ cấp ngân sách.
    ///
    /// Không cần chính xác tới từng xu — nó dùng để biết khi nào phải hạ cấp, chứ không
    /// dùng để xuất hoá đơn.
    /// </summary>
    private static decimal EstimateCost(AiProvider provider, int inputTokens, int outputTokens)
    {
        // USD trên một triệu token, (đầu vào, đầu ra).
        var (inputRate, outputRate) = provider switch
        {
            AiProvider.Anthropic => (3.00m, 15.00m),
            AiProvider.OpenAi => (2.50m, 10.00m),
            AiProvider.Gemini => (0.30m, 2.50m),
            AiProvider.OpenRouter => (1.00m, 3.00m),
            AiProvider.AzureOpenAi => (2.50m, 10.00m),

            // Chạy tại chỗ nên không mất tiền gọi API.
            AiProvider.Ollama => (0m, 0m),
            _ => (1.00m, 3.00m),
        };

        return (inputTokens * inputRate + outputTokens * outputRate) / 1_000_000m;
    }

    private async Task LogUsageAsync(
        AiRequest request, AiProvider provider, string model, int inputTokens, int outputTokens,
        decimal cost, bool cacheHit, bool succeeded, string? errorCode, int latencyMs, CancellationToken ct)
    {
        db.AiUsages.Add(new AiUsage
        {
            UserId = request.UserId,
            TaskName = request.TaskName,
            Tier = request.Tier.ToString(),
            Provider = provider,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCostUsd = cost,
            CacheHit = cacheHit,
            Succeeded = succeeded,

            // Mã rút gọn, không bao giờ là thông điệp thô của nhà cung cấp.
            ErrorCode = errorCode,
            LatencyMs = latencyMs,
        });

        await Task.CompletedTask;
    }
}
