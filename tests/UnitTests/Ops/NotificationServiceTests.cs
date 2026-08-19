using EnglishForIT.Application.Ops;
using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Ops;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnglishForIT.UnitTests.Ops;

/// <summary>
/// Ba luật chặn thông báo.
///
/// Cái đáng lo nhất không phải thông báo không gửi được mà là gửi quá nhiều: một lần chốt bài
/// làm engine tính lại downstream và học viên nhận mười thông báo "bài đã mở" liền nhau,
/// rồi tắt thông báo vĩnh viễn.
/// </summary>
public class NotificationServiceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 19, 5, 0, 0, TimeSpan.Zero);   // 12 giờ trưa VN

    [Fact]
    public async Task GuiDuocKhiChuaCoGiChan()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);

        var sent = await NewService(db).PushAsync(userId, AnyNotification(), Noon);
        await db.SaveChangesAsync();

        Assert.True(sent);
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task CungKhoaGopThiChiTaoMotBanGhi()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);
        var service = NewService(db);

        // Engine tính lại downstream có thể gọi hàng chục lần cho cùng một bài.
        for (var i = 0; i < 10; i++)
        {
            await service.PushAsync(userId, NotificationComposer.LessonUnlocked("INF-03", "Đọc log"), Noon);
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task TrongGioKhongLamPhienThiKhongGui()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);

        db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = userId,
            QuietHoursStart = 22,
            QuietHoursEnd = 7,
        });
        await db.SaveChangesAsync();

        // 18 giờ UTC = 1 giờ sáng ở Việt Nam, nằm trong khoảng 22-7.
        var midnight = new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero);

        var sent = await NewService(db).PushAsync(userId, AnyNotification(), midnight);

        Assert.False(sent);
    }

    [Fact]
    public async Task ThongBaoDoChinhHocVienGayRaThiBoQuaGioYenTinh()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);

        db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = userId,
            QuietHoursStart = 22,
            QuietHoursEnd = 7,
        });
        await db.SaveChangesAsync();

        var midnight = new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero);

        // Họ vừa chốt bài lúc 1 giờ sáng nên đang mở app: chặn theo giờ yên tĩnh là vô nghĩa.
        var sent = await NewService(db).PushAsync(
            userId, NotificationComposer.LessonUnlocked("INF-03", "Đọc log"), midnight, ignoreQuietHours: true);

        Assert.True(sent);
    }

    [Fact]
    public async Task TatLoaiNaoThiKhongNhanLoaiDo()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);

        db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = userId,
            DailyReminderEnabled = false,
            QuietHoursStart = 0,
            QuietHoursEnd = 0,
        });
        await db.SaveChangesAsync();

        var service = NewService(db);

        Assert.False(await service.PushAsync(
            userId, NotificationComposer.DailyReminder(new DateOnly(2026, 8, 19), 45), Noon));

        // Nhưng bài mở khoá vẫn phải tới: đó là tiến độ, không phải quảng cáo.
        Assert.True(await service.PushAsync(
            userId, NotificationComposer.LessonUnlocked("INF-03", "Đọc log"), Noon));
    }

    [Fact]
    public async Task ThuTrungKhoaChongGuiTrungThiKhongXepHaiLan()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);
        var service = NewService(db);

        for (var i = 0; i < 3; i++)
        {
            await service.QueueEmailAsync(userId, "a@vidu.vn", "Tuần vừa rồi", "<p>x</p>", "x", "weekly:2026-W34");
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await db.OutboxEmails.CountAsync());
    }

    [Fact]
    public async Task TatEmailThiKhongXepThuNao()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);

        db.NotificationPreferences.Add(new NotificationPreference { UserId = userId, EmailEnabled = false });
        await db.SaveChangesAsync();

        var queued = await NewService(db).QueueEmailAsync(
            userId, "a@vidu.vn", "Tuần vừa rồi", "<p>x</p>", "x", "weekly:2026-W34");

        Assert.False(queued);
    }

    // -----------------------------------------------------------------------

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"notify-{Guid.NewGuid()}")
            .Options);

    private static NotificationService NewService(AppDbContext db) =>
        new(db, NullLogger<NotificationService>.Instance);

    private static ComposedNotification AnyNotification() =>
        NotificationComposer.DailyReminder(new DateOnly(2026, 8, 19), 45);

    private static async Task<Guid> SeedUserAsync(AppDbContext db)
    {
        var userId = Guid.CreateVersion7();
        db.UserProfiles.Add(new UserProfile { UserId = userId, TimeZone = "Asia/Ho_Chi_Minh" });
        await db.SaveChangesAsync();
        return userId;
    }
}

/// <summary>Khoảng giờ không làm phiền hay vắt qua nửa đêm — chỗ này rất dễ viết sai.</summary>
public class QuietHourTests
{
    [Theory]
    [InlineData(23, 22, 7, true)]    // trong khoảng, trước nửa đêm
    [InlineData(3, 22, 7, true)]     // trong khoảng, sau nửa đêm
    [InlineData(7, 22, 7, false)]    // đúng giờ kết thúc là đã hết
    [InlineData(21, 22, 7, false)]
    [InlineData(12, 22, 7, false)]
    [InlineData(13, 9, 17, true)]    // khoảng không vắt qua nửa đêm
    [InlineData(8, 9, 17, false)]
    [InlineData(5, 0, 0, false)]     // bắt đầu trùng kết thúc nghĩa là tắt
    public void TinhDungKhoangVatQuaNuaDem(int hour, int start, int end, bool expected)
    {
        Assert.Equal(expected, Application.Learning.LocalDay.IsQuietHour(hour, start, end));
    }
}
