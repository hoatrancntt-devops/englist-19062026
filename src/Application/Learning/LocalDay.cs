namespace EnglishForIT.Application.Learning;

/// <summary>
/// Đổi mốc thời gian UTC sang ngày theo lịch địa phương của học viên.
///
/// Tách ra một chỗ vì trước đây mỗi nơi tự cộng 7 tiếng. Cách đó đúng với người ở Việt Nam
/// và sai với mọi người khác — mà chuỗi ngày, giờ nhắc học và giờ không làm phiền đều
/// dựa trên con số này, nên sai ở đây là sai cả ba.
/// </summary>
public static class LocalDay
{
    /// <summary>Múi giờ dùng khi hồ sơ không khai hoặc khai sai.</summary>
    public const string DefaultTimeZone = "Asia/Ho_Chi_Minh";

    public static TimeZoneInfo Resolve(string? ianaId)
    {
        // Id lạ không được phép ném lỗi: một hồ sơ hỏng không nên làm job nhắc học
        // dừng cho toàn bộ học viên còn lại.
        if (!string.IsNullOrWhiteSpace(ianaId) && TryFind(ianaId, out var zone))
        {
            return zone;
        }

        return TryFind(DefaultTimeZone, out var fallback) ? fallback : TimeZoneInfo.Utc;
    }

    public static DateTimeOffset ToLocal(DateTimeOffset utc, string? ianaId) =>
        TimeZoneInfo.ConvertTime(utc, Resolve(ianaId));

    public static DateOnly DateFor(DateTimeOffset utc, string? ianaId) =>
        DateOnly.FromDateTime(ToLocal(utc, ianaId).DateTime);

    public static int HourFor(DateTimeOffset utc, string? ianaId) =>
        ToLocal(utc, ianaId).Hour;

    /// <summary>
    /// Giờ này có nằm trong khoảng không làm phiền không.
    ///
    /// Khoảng thường vắt qua nửa đêm (22 giờ tới 7 giờ) nên không so sánh thẳng được;
    /// đó cũng là lỗi hay gặp nhất khi tự viết lại hàm này.
    /// </summary>
    public static bool IsQuietHour(int hour, int quietStart, int quietEnd)
    {
        if (quietStart == quietEnd)
        {
            return false;
        }

        return quietStart < quietEnd
            ? hour >= quietStart && hour < quietEnd
            : hour >= quietStart || hour < quietEnd;
    }

    private static bool TryFind(string id, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
