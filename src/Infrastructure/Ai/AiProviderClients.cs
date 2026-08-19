using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EnglishForIT.Application.Abstractions;
using EnglishForIT.Application.Ai;
using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Infrastructure.Ai;

/// <summary>
/// Client cho các API theo hình dạng OpenAI chat completions.
///
/// Bốn trong sáu nhà cung cấp dùng chung hình dạng này: OpenAI, OpenRouter, Azure OpenAI
/// và Ollama. Viết bốn class gần giống hệt nhau chỉ khác base URL là cách chắc chắn để
/// sửa lỗi ở một chỗ và quên ba chỗ còn lại.
/// </summary>
public abstract class OpenAiCompatibleClient(IHttpClientFactory httpFactory, ISecretProtector secrets)
    : IAiProviderClient
{
    public abstract AiProvider Provider { get; }

    protected abstract string DefaultBaseUrl { get; }

    /// <summary>Model cho từng tầng. T1 rẻ và nhanh, T2 mạnh và tốn.</summary>
    protected abstract string ModelFor(AiTier tier);

    /// <summary>Ollama không cần khoá; các nhà còn lại thì cần.</summary>
    protected virtual bool RequiresApiKey => true;

    public async Task<AiProviderResult> CompleteAsync(
        AiProviderSetting setting, AiRequest request, AiTier tier, CancellationToken ct)
    {
        var model = ModelFor(tier);
        var client = httpFactory.CreateClient(nameof(OpenAiCompatibleClient));

        client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(setting.BaseUrl) ? DefaultBaseUrl : setting.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(45);

        if (RequiresApiKey)
        {
            if (string.IsNullOrWhiteSpace(setting.ApiKeyEncrypted))
            {
                return new AiProviderResult(false, null, model, 0, 0, "missing_api_key");
            }

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", secrets.Unprotect(setting.ApiKeyEncrypted));
        }

        var response = await client.PostAsJsonAsync("chat/completions", new
        {
            model,
            max_tokens = request.MaxOutputTokens,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt },
            },
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            // Chỉ ghi mã trạng thái. Thân phản hồi lỗi của nhiều nhà cung cấp có kèm lại
            // nguyên prompt, mà usage log thì hiện trên màn quản trị.
            return new AiProviderResult(false, null, model, 0, 0, $"http_{(int)response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var text = root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
            ? choices[0].GetProperty("message").GetProperty("content").GetString()
            : null;

        var (input, output) = ReadUsage(root);

        return string.IsNullOrWhiteSpace(text)
            ? new AiProviderResult(false, null, model, input, output, "empty_response")
            : new AiProviderResult(true, text, model, input, output, null);
    }

    private static (int Input, int Output) ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage))
        {
            return (0, 0);
        }

        return (
            usage.TryGetProperty("prompt_tokens", out var i) ? i.GetInt32() : 0,
            usage.TryGetProperty("completion_tokens", out var o) ? o.GetInt32() : 0);
    }
}

public class OpenAiClient(IHttpClientFactory f, ISecretProtector s) : OpenAiCompatibleClient(f, s)
{
    public override AiProvider Provider => AiProvider.OpenAi;
    protected override string DefaultBaseUrl => "https://api.openai.com/v1/";
    protected override string ModelFor(AiTier tier) => tier == AiTier.T1 ? "gpt-4o-mini" : "gpt-4o";
}

public class OpenRouterClient(IHttpClientFactory f, ISecretProtector s) : OpenAiCompatibleClient(f, s)
{
    public override AiProvider Provider => AiProvider.OpenRouter;
    protected override string DefaultBaseUrl => "https://openrouter.ai/api/v1/";
    protected override string ModelFor(AiTier tier) =>
        tier == AiTier.T1 ? "google/gemini-2.0-flash-001" : "anthropic/claude-3.7-sonnet";
}

public class AzureOpenAiClient(IHttpClientFactory f, ISecretProtector s) : OpenAiCompatibleClient(f, s)
{
    public override AiProvider Provider => AiProvider.AzureOpenAi;

    // Azure luôn cần base URL riêng của tenant, nên giá trị này chỉ là chỗ giữ chân —
    // không cấu hình BaseUrl thì lời gọi sẽ hỏng và gateway chuyển sang nhà kế tiếp.
    protected override string DefaultBaseUrl => "https://example.openai.azure.com/openai/v1/";
    protected override string ModelFor(AiTier tier) => tier == AiTier.T1 ? "gpt-4o-mini" : "gpt-4o";
}

/// <summary>
/// Ollama chạy tại chỗ: không khoá API, không tốn tiền gọi.
///
/// Đây là nhà cung cấp duy nhất kiểm chứng được hoàn toàn trên máy dev mà không cần
/// đăng ký tài khoản ở đâu.
/// </summary>
public class OllamaClient(IHttpClientFactory f, ISecretProtector s) : OpenAiCompatibleClient(f, s)
{
    public override AiProvider Provider => AiProvider.Ollama;
    protected override string DefaultBaseUrl => "http://ollama:11434/v1/";
    protected override bool RequiresApiKey => false;
    protected override string ModelFor(AiTier tier) => tier == AiTier.T1 ? "qwen2.5:0.5b" : "qwen2.5:3b";
}

/// <summary>Anthropic dùng hình dạng riêng: /v1/messages, header x-api-key, và system tách khỏi messages.</summary>
public class AnthropicClient(IHttpClientFactory httpFactory, ISecretProtector secrets) : IAiProviderClient
{
    public AiProvider Provider => AiProvider.Anthropic;

    public async Task<AiProviderResult> CompleteAsync(
        AiProviderSetting setting, AiRequest request, AiTier tier, CancellationToken ct)
    {
        var model = tier == AiTier.T1 ? "claude-haiku-4-5-20251001" : "claude-sonnet-5";

        if (string.IsNullOrWhiteSpace(setting.ApiKeyEncrypted))
        {
            return new AiProviderResult(false, null, model, 0, 0, "missing_api_key");
        }

        var client = httpFactory.CreateClient(nameof(AnthropicClient));
        client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(setting.BaseUrl) ? "https://api.anthropic.com/v1/" : setting.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(45);
        client.DefaultRequestHeaders.Add("x-api-key", secrets.Unprotect(setting.ApiKeyEncrypted));
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await client.PostAsJsonAsync("messages", new
        {
            model,
            max_tokens = request.MaxOutputTokens,
            system = request.SystemPrompt,
            messages = new object[] { new { role = "user", content = request.UserPrompt } },
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            return new AiProviderResult(false, null, model, 0, 0, $"http_{(int)response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var text = root.TryGetProperty("content", out var content) && content.GetArrayLength() > 0
            ? content[0].GetProperty("text").GetString()
            : null;

        var input = root.TryGetProperty("usage", out var usage) && usage.TryGetProperty("input_tokens", out var i)
            ? i.GetInt32() : 0;
        var output = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("output_tokens", out var o)
            ? o.GetInt32() : 0;

        return string.IsNullOrWhiteSpace(text)
            ? new AiProviderResult(false, null, model, input, output, "empty_response")
            : new AiProviderResult(true, text, model, input, output, null);
    }
}

/// <summary>Gemini dùng generateContent và đưa khoá vào query string.</summary>
public class GeminiClient(IHttpClientFactory httpFactory, ISecretProtector secrets) : IAiProviderClient
{
    public AiProvider Provider => AiProvider.Gemini;

    public async Task<AiProviderResult> CompleteAsync(
        AiProviderSetting setting, AiRequest request, AiTier tier, CancellationToken ct)
    {
        var model = tier == AiTier.T1 ? "gemini-2.0-flash" : "gemini-2.5-pro";

        if (string.IsNullOrWhiteSpace(setting.ApiKeyEncrypted))
        {
            return new AiProviderResult(false, null, model, 0, 0, "missing_api_key");
        }

        var client = httpFactory.CreateClient(nameof(GeminiClient));
        client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(setting.BaseUrl)
            ? "https://generativelanguage.googleapis.com/v1beta/"
            : setting.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(45);

        // Khoá đi trong header thay vì query string: query string bị ghi vào access log
        // của mọi proxy trên đường đi.
        client.DefaultRequestHeaders.Add("x-goog-api-key", secrets.Unprotect(setting.ApiKeyEncrypted));

        var response = await client.PostAsJsonAsync($"models/{model}:generateContent", new
        {
            system_instruction = new { parts = new[] { new { text = request.SystemPrompt } } },
            contents = new[] { new { parts = new[] { new { text = request.UserPrompt } } } },
            generationConfig = new { maxOutputTokens = request.MaxOutputTokens },
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            return new AiProviderResult(false, null, model, 0, 0, $"http_{(int)response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var text = root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0
            ? candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
            : null;

        var input = root.TryGetProperty("usageMetadata", out var usage)
                    && usage.TryGetProperty("promptTokenCount", out var i) ? i.GetInt32() : 0;
        var output = usage.ValueKind == JsonValueKind.Object
                     && usage.TryGetProperty("candidatesTokenCount", out var o) ? o.GetInt32() : 0;

        return string.IsNullOrWhiteSpace(text)
            ? new AiProviderResult(false, null, model, input, output, "empty_response")
            : new AiProviderResult(true, text, model, input, output, null);
    }
}
