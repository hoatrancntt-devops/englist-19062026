namespace EnglishForIT.Application.Content;

/// <summary>
/// Danh mục hình minh hoạ dùng được trong bài học.
///
/// Đây là danh sách đóng, có chủ đích. Cho phép đặt khoá tự do sẽ dẫn tới
/// hai hậu quả: gõ sai một chữ là bài không có hình mà không ai biết,
/// và mỗi người soạn lại nghĩ ra một tên riêng cho cùng một cảnh.
///
/// Mỗi khoá tương ứng một component SVG trong apps/web/src/components/illustrations/
/// scene-illustrations.tsx. Thêm khoá ở đây thì PHẢI thêm component tương ứng,
/// nếu không giao diện sẽ rơi về hình mặc định.
/// </summary>
public static class IllustrationCatalogue
{
    /// <summary>Cảnh đời sống và giao tiếp chung.</summary>
    public const string CoffeeChat = "coffee-chat";
    public const string PhoneCall = "phone-call";
    public const string ClockCalendar = "clock-calendar";
    public const string ConfusedListener = "confused-listener";
    public const string HandshakeIntro = "handshake-intro";
    public const string FoodOrder = "food-order";
    public const string CityTransport = "city-transport";
    public const string MapDirections = "map-directions";
    public const string MoneyReceipt = "money-receipt";

    /// <summary>Cảnh văn phòng.</summary>
    public const string DeskLaptop = "desk-laptop";
    public const string TeamStandup = "team-standup";
    public const string EmailInbox = "email-inbox";
    public const string ChatMessage = "chat-message";
    public const string VideoCall = "video-call";

    /// <summary>Cảnh chuyên môn.</summary>
    public const string TicketQueue = "ticket-queue";
    public const string ServerRack = "server-rack";
    public const string OutageAlert = "outage-alert";
    public const string NetworkDiagram = "network-diagram";
    public const string BackupRestore = "backup-restore";
    public const string ShieldLock = "shield-lock";

    /// <summary>Cảnh cloud.</summary>
    public const string CloudStack = "cloud-stack";
    public const string HaFailover = "ha-failover";
    public const string ScaleGraph = "scale-graph";
    public const string ArchitectureReview = "architecture-review";
    public const string CloudMigration = "cloud-migration";

    /// <summary>Cảnh AI.</summary>
    public const string AiBrain = "ai-brain";
    public const string DataPipeline = "data-pipeline";
    public const string MetricsDashboard = "metrics-dashboard";
    public const string RiskMatrix = "risk-matrix";

    /// <summary>Cảnh đọc tài liệu kỹ thuật.</summary>
    public const string LogLines = "log-lines";
    public const string ReleaseNotes = "release-notes";
    public const string ApiDoc = "api-doc";
    public const string PostmortemTimeline = "postmortem-timeline";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        CoffeeChat, PhoneCall, ClockCalendar, ConfusedListener, HandshakeIntro,
        FoodOrder, CityTransport, MapDirections, MoneyReceipt,
        DeskLaptop, TeamStandup, EmailInbox, ChatMessage, VideoCall,
        TicketQueue, ServerRack, OutageAlert, NetworkDiagram, BackupRestore, ShieldLock,
        CloudStack, HaFailover, ScaleGraph, ArchitectureReview, CloudMigration,
        AiBrain, DataPipeline, MetricsDashboard, RiskMatrix,
        LogLines, ReleaseNotes, ApiDoc, PostmortemTimeline,
    };

    public static bool IsKnown(string key) => Known.Contains(key);

    public static IReadOnlyCollection<string> All => Known;
}
