namespace EnglishForIT.Domain.Enums;

public enum NotificationType
{
    DailyReminder = 0,
    StreakWarning = 1,
    StreakLost = 2,
    CheckpointPassed = 3,
    LessonUnlocked = 4,
    ReviewDue = 5,
    RetentionDebt = 6,
    WeeklyReport = 7,
    PlacementReady = 8
}

public enum OutboxStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public enum MailProvider
{
    MicrosoftGraph = 0,
    Smtp = 1
}

public enum AiProvider
{
    Anthropic = 0,
    OpenAi = 1,
    Gemini = 2,
    OpenRouter = 3,
    Ollama = 4,
    AzureOpenAi = 5
}

/// <summary>Chế độ ngân sách AI. Vượt ngưỡng thì tự hạ cấp thay vì tắt đột ngột.</summary>
public enum AiBudgetMode
{
    /// <summary>Dưới 70% trần tháng: chạy đủ T1 và T2.</summary>
    Normal = 0,

    /// <summary>70 đến 90 phần trăm: T2 hạ xuống T1, cache TTL nhân đôi.</summary>
    Degraded = 1,

    /// <summary>Trên 90%: chỉ trả cache, còn lại dùng fallback rule-based.</summary>
    CacheOnly = 2
}

/// <summary>
/// Lý do một bài đổi trạng thái. Ghi lại để màn hình "vì sao bài này bị khoá"
/// có dữ liệu thật thay vì đoán ngược từ điểm số.
/// </summary>
public enum LessonStateReason
{
    PlacementUnlock = 0,
    PrerequisiteMet = 1,
    PrerequisiteNotMet = 2,
    SkillBelowThreshold = 3,
    SpeakingGateNotPassed = 4,
    ChallengePassed = 5,
    MasteryReached = 6,
    RetentionDecay = 7,
    ManualAdminOverride = 8,

    /// <summary>
    /// Trượt thi vượt. Trạng thái bài KHÔNG đổi, nhưng vẫn phải ghi lại: đây là nguồn
    /// duy nhất để tính khoảng chờ trước lần thi lại.
    /// </summary>
    ChallengeFailed = 9
}
