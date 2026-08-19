using EnglishForIT.Domain.Common;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Domain.Entities.Ops;

/// <summary>
/// Cache phản hồi AI theo hash của (task, phiên bản prompt, payload).
/// Đây là thứ giữ chi phí AI ở mức chấp nhận được.
/// </summary>
public class AiCacheEntry : Entity
{
    /// <summary>SHA-256 hex của khoá tổng hợp.</summary>
    public required string CacheKey { get; set; }

    public required string TaskName { get; set; }
    public required string PromptVersion { get; set; }

    public required string ResponseJson { get; set; }

    public AiProvider Provider { get; set; }
    public required string Model { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int HitCount { get; set; }
    public DateTimeOffset? LastHitAt { get; set; }
}

/// <summary>Nhật ký gọi AI. Dữ liệu để tính chi phí và quyết định hạ cấp ngân sách.</summary>
public class AiUsage : Entity
{
    public Guid? UserId { get; set; }

    public required string TaskName { get; set; }
    public required string Tier { get; set; }

    public AiProvider Provider { get; set; }
    public required string Model { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    /// <summary>Chi phí ước tính, USD. Tra từ bảng giá trong cấu hình.</summary>
    public decimal EstimatedCostUsd { get; set; }

    public bool CacheHit { get; set; }
    public bool Succeeded { get; set; }

    /// <summary>Mã lỗi rút gọn khi thất bại. Không bao giờ chứa nội dung prompt hay khoá API.</summary>
    public string? ErrorCode { get; set; }

    public int LatencyMs { get; set; }
}

/// <summary>
/// Cấu hình một nhà cung cấp AI. Khoá API mã hoá bằng khoá dẫn xuất từ bí mật ứng dụng.
/// </summary>
public class AiProviderSetting : Entity
{
    public AiProvider Provider { get; set; }

    public bool Enabled { get; set; }

    /// <summary>Đã mã hoá. API không bao giờ trả nguyên văn, chỉ trả dạng che.</summary>
    public string? ApiKeyEncrypted { get; set; }

    /// <summary>Endpoint tuỳ biến, cần cho Ollama và Azure OpenAI.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Cấu hình thêm theo provider, JSONB. Ví dụ deployment name của Azure.</summary>
    public string? ExtraJson { get; set; }

    public DateTimeOffset? LastVerifiedAt { get; set; }
    public bool? LastVerifySucceeded { get; set; }
}

/// <summary>
/// Nhật ký kiểm toán. Ghi tên hành động và đối tượng, tuyệt đối không ghi giá trị bí mật.
/// </summary>
public class AuditLog : Entity
{
    public Guid? ActorUserId { get; set; }

    /// <summary>Ví dụ ai_provider.key_updated, content.published, user.role_granted.</summary>
    public required string Action { get; set; }

    public required string TargetType { get; set; }
    public string? TargetId { get; set; }

    /// <summary>
    /// Thông tin bổ sung, JSONB. Có bộ lọc chặn khoá tên nhạy cảm
    /// (key, secret, password, token) trước khi ghi.
    /// </summary>
    public string? MetadataJson { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>Cấu hình hệ thống dạng khoá-giá trị, sửa được trên web mà không cần deploy lại.</summary>
public class SystemSetting : Entity
{
    public required string Key { get; set; }
    public required string ValueJson { get; set; }

    /// <summary>Giá trị nhạy cảm thì mã hoá và che khi trả ra API.</summary>
    public bool IsSecret { get; set; }

    public string? DescriptionVi { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
