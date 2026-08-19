using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Ai;

/// <summary>
/// Tầng chất lượng của một tác vụ AI.
///
/// T1 là việc rẻ và ngắn (gợi ý một câu, phân loại). T2 là việc dài và tốn (sinh bài,
/// nhận xét chi tiết). Chia tầng để lúc gần chạm trần ngân sách còn có thứ để hạ cấp
/// thay vì tắt sạch.
/// </summary>
public enum AiTier
{
    T1 = 1,
    T2 = 2,
}

public record AiRequest(
    /// <summary>Tên tác vụ, dùng cho cache và thống kê chi phí. Ví dụ "feedback.writing".</summary>
    string TaskName,

    /// <summary>Đổi khi sửa prompt. Prompt đổi mà không đổi số này thì cache trả về câu trả lời cũ.</summary>
    string PromptVersion,

    AiTier Tier,
    string SystemPrompt,
    string UserPrompt,

    /// <summary>Thời gian giữ cache. Câu trả lời cho cùng một đầu vào hiếm khi cần tươi.</summary>
    TimeSpan CacheFor,

    Guid? UserId = null,
    int MaxOutputTokens = 1024);

public record AiResponse(
    string Text,
    bool FromCache,
    AiProvider? Provider,
    string? Model,

    /// <summary>
    /// True khi câu trả lời do luật sinh chứ không phải AI: hết ngân sách, chưa cấu hình
    /// nhà cung cấp nào, hoặc mọi nhà cung cấp đều lỗi.
    /// </summary>
    bool FromFallback,

    string? ErrorCode = null);

/// <summary>
/// Quyết định ngân sách.
///
/// Thuần tính toán, không chạm DB — đây là quy tắc sản phẩm nên phải đọc được bằng mắt
/// và test được không cần hạ tầng.
/// </summary>
public static class AiBudgetPolicy
{
    /// <summary>Dưới ngưỡng này thì chạy bình thường.</summary>
    public const double DegradedThreshold = 0.70;

    /// <summary>Trên ngưỡng này thì chỉ còn cache và fallback.</summary>
    public const double CacheOnlyThreshold = 0.90;

    public static AiBudgetMode ModeFor(decimal spentUsd, decimal monthlyCapUsd)
    {
        // Không đặt trần nghĩa là không giới hạn, không phải là chặn hết.
        if (monthlyCapUsd <= 0)
        {
            return AiBudgetMode.Normal;
        }

        var ratio = (double)(spentUsd / monthlyCapUsd);

        return ratio switch
        {
            >= CacheOnlyThreshold => AiBudgetMode.CacheOnly,
            >= DegradedThreshold => AiBudgetMode.Degraded,
            _ => AiBudgetMode.Normal,
        };
    }

    /// <summary>
    /// Tầng thực tế sau khi áp chế độ ngân sách.
    ///
    /// Ở chế độ Degraded, T2 bị hạ xuống T1: câu trả lời ngắn hơn nhưng vẫn là câu trả lời,
    /// tốt hơn hẳn việc tắt đột ngột giữa tháng.
    /// </summary>
    public static AiTier EffectiveTier(AiTier requested, AiBudgetMode mode) =>
        mode == AiBudgetMode.Degraded ? AiTier.T1 : requested;

    /// <summary>Ở chế độ Degraded, cache giữ lâu gấp đôi để giảm số lần gọi thật.</summary>
    public static TimeSpan EffectiveCacheDuration(TimeSpan requested, AiBudgetMode mode) =>
        mode == AiBudgetMode.Degraded ? requested * 2 : requested;

    /// <summary>Chế độ này còn được phép gọi nhà cung cấp không.</summary>
    public static bool CanCallProvider(AiBudgetMode mode) => mode != AiBudgetMode.CacheOnly;
}
