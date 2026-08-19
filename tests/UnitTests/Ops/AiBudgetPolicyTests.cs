using EnglishForIT.Application.Ai;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.UnitTests.Ops;

/// <summary>
/// Luật ngân sách AI.
///
/// Mục tiêu của luật này là <b>không bao giờ tắt đột ngột giữa tháng</b>. Chi phí tăng dần
/// thì chất lượng giảm dần: T2 hạ xuống T1, cache giữ lâu gấp đôi, cuối cùng mới chỉ còn
/// cache và fallback. Học viên luôn nhận được câu trả lời, chỉ là ngắn hơn.
/// </summary>
public class AiBudgetPolicyTests
{
    [Theory]
    [InlineData(0, 100, AiBudgetMode.Normal)]
    [InlineData(69, 100, AiBudgetMode.Normal)]
    [InlineData(70, 100, AiBudgetMode.Degraded)]
    [InlineData(89, 100, AiBudgetMode.Degraded)]
    [InlineData(90, 100, AiBudgetMode.CacheOnly)]
    [InlineData(200, 100, AiBudgetMode.CacheOnly)]
    public void ChonDungCheDoTheoTyLeDaChi(decimal spent, decimal cap, AiBudgetMode expected)
    {
        Assert.Equal(expected, AiBudgetPolicy.ModeFor(spent, cap));
    }

    [Fact]
    public void KhongDatTranThiKhongGioiHan()
    {
        // Trần 0 nghĩa là "không đặt giới hạn", KHÔNG phải "chặn hết".
        // Hiểu nhầm chỗ này sẽ làm mọi tác vụ AI im lặng rơi về fallback.
        Assert.Equal(AiBudgetMode.Normal, AiBudgetPolicy.ModeFor(1000m, 0m));
    }

    [Fact]
    public void CheDoHaCapThiT2XuongT1()
    {
        Assert.Equal(AiTier.T1, AiBudgetPolicy.EffectiveTier(AiTier.T2, AiBudgetMode.Degraded));
        Assert.Equal(AiTier.T1, AiBudgetPolicy.EffectiveTier(AiTier.T1, AiBudgetMode.Degraded));
    }

    [Fact]
    public void CheDoBinhThuongVaChiCacheThiGiuNguyenTang()
    {
        Assert.Equal(AiTier.T2, AiBudgetPolicy.EffectiveTier(AiTier.T2, AiBudgetMode.Normal));

        // Ở CacheOnly thì tầng không còn ý nghĩa vì không gọi nhà cung cấp nữa,
        // nhưng cũng không được âm thầm đổi giá trị.
        Assert.Equal(AiTier.T2, AiBudgetPolicy.EffectiveTier(AiTier.T2, AiBudgetMode.CacheOnly));
    }

    [Fact]
    public void CheDoHaCapThiCacheGiuLauGapDoi()
    {
        var oneHour = TimeSpan.FromHours(1);

        Assert.Equal(TimeSpan.FromHours(2), AiBudgetPolicy.EffectiveCacheDuration(oneHour, AiBudgetMode.Degraded));
        Assert.Equal(oneHour, AiBudgetPolicy.EffectiveCacheDuration(oneHour, AiBudgetMode.Normal));
    }

    [Fact]
    public void ChiCacheThiKhongGoiNhaCungCapNua()
    {
        Assert.True(AiBudgetPolicy.CanCallProvider(AiBudgetMode.Normal));
        Assert.True(AiBudgetPolicy.CanCallProvider(AiBudgetMode.Degraded));
        Assert.False(AiBudgetPolicy.CanCallProvider(AiBudgetMode.CacheOnly));
    }
}
