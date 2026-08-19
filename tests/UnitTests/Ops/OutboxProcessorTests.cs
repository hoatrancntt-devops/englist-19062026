using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Ops;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnglishForIT.UnitTests.Ops;

/// <summary>
/// Hộp thư đi.
///
/// Điểm cần canh không phải đường gửi thành công mà là đường hỏng: thư phải thử lại có
/// giãn cách, và phải BỎ CUỘC sau một số lần. Thư kẹt mãi trong hàng đợi còn tệ hơn thư mất,
/// vì không ai biết nó tồn tại.
/// </summary>
public class OutboxProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GuiThanhCongThiDanhDauSent()
    {
        var db = NewDb();
        await SeedEmailAsync(db, "a@vidu.vn");

        var sent = await NewProcessor(db, alwaysSucceed: true).ProcessAsync(Now);

        var email = await db.OutboxEmails.SingleAsync();

        Assert.Equal(1, sent);
        Assert.Equal(OutboxStatus.Sent, email.Status);
        Assert.Equal(Now, email.SentAt);
        Assert.Null(email.LastError);
    }

    [Fact]
    public async Task GuiHongThiLuiLichChuKhongThuLaiNgay()
    {
        var db = NewDb();
        await SeedEmailAsync(db, "a@vidu.vn");

        await NewProcessor(db, alwaysSucceed: false).ProcessAsync(Now);

        var email = await db.OutboxEmails.SingleAsync();

        Assert.Equal(OutboxStatus.Pending, email.Status);
        Assert.Equal(1, email.AttemptCount);
        Assert.Equal(Now.AddMinutes(1), email.NextAttemptAt);
        Assert.NotNull(email.LastError);
    }

    [Fact]
    public async Task ChuaToiLichThiKhongDungToi()
    {
        var db = NewDb();
        await SeedEmailAsync(db, "a@vidu.vn", nextAttemptAt: Now.AddMinutes(10));

        var sent = await NewProcessor(db, alwaysSucceed: true).ProcessAsync(Now);

        Assert.Equal(0, sent);
        Assert.Equal(0, (await db.OutboxEmails.SingleAsync()).AttemptCount);
    }

    [Fact]
    public async Task QuaSoLanThuThiBoCuoc()
    {
        var db = NewDb();
        await SeedEmailAsync(db, "a@vidu.vn", attemptCount: OutboxProcessor.MaxAttempts - 1);

        await NewProcessor(db, alwaysSucceed: false).ProcessAsync(Now);

        var email = await db.OutboxEmails.SingleAsync();

        Assert.Equal(OutboxStatus.Failed, email.Status);
        Assert.Equal(OutboxProcessor.MaxAttempts, email.AttemptCount);
    }

    [Fact]
    public void LichLuiTangTheoCapSoNhan()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), OutboxProcessor.BackoffFor(1));
        Assert.Equal(TimeSpan.FromMinutes(5), OutboxProcessor.BackoffFor(2));
        Assert.Equal(TimeSpan.FromMinutes(25), OutboxProcessor.BackoffFor(3));
        Assert.Equal(TimeSpan.FromMinutes(125), OutboxProcessor.BackoffFor(4));
    }

    [Fact]
    public async Task LoiDaiBiCatNganTruocKhiGhi()
    {
        var db = NewDb();
        await SeedEmailAsync(db, "a@vidu.vn");

        var processor = new OutboxProcessor(
            db, new StubSender(false, new string('x', 5000)), NullLogger<OutboxProcessor>.Instance);

        await processor.ProcessAsync(Now);

        // Cột này hiện trên màn quản trị, không được mang theo thứ gì dài bất thường.
        Assert.Equal(500, (await db.OutboxEmails.SingleAsync()).LastError!.Length);
    }

    [Fact]
    public async Task MotLuotChiXuLyToiDaMotMe()
    {
        var db = NewDb();

        for (var i = 0; i < OutboxProcessor.BatchSize + 5; i++)
        {
            await SeedEmailAsync(db, $"a{i}@vidu.vn", key: $"k{i}");
        }

        var sent = await NewProcessor(db, alwaysSucceed: true).ProcessAsync(Now);

        Assert.Equal(OutboxProcessor.BatchSize, sent);
    }

    // -----------------------------------------------------------------------

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"outbox-{Guid.NewGuid()}")
            .Options);

    private static OutboxProcessor NewProcessor(AppDbContext db, bool alwaysSucceed) =>
        new(db, new StubSender(alwaysSucceed, "SMTP tu choi"), NullLogger<OutboxProcessor>.Instance);

    private static async Task SeedEmailAsync(
        AppDbContext db,
        string to,
        DateTimeOffset? nextAttemptAt = null,
        int attemptCount = 0,
        string key = "k1")
    {
        db.OutboxEmails.Add(new OutboxEmail
        {
            ToAddress = to,
            Subject = "Thử",
            HtmlBody = "<p>x</p>",
            TextBody = "x",
            IdempotencyKey = key,
            NextAttemptAt = nextAttemptAt,
            AttemptCount = attemptCount,
        });

        await db.SaveChangesAsync();
    }

    private sealed class StubSender(bool success, string error) : IEmailSender
    {
        public Task<EmailSendResult> SendAsync(OutboxEmail email, CancellationToken ct = default) =>
            Task.FromResult(new EmailSendResult(success, success ? null : error));
    }
}
