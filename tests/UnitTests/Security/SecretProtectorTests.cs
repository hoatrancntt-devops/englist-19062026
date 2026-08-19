using EnglishForIT.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EnglishForIT.UnitTests.Security;

public class AesGcmSecretProtectorTests
{
    private const string KhoaGoc = "khoa-test-du-dai-de-vuot-nguong-32-ky-tu-0000";

    private static AesGcmSecretProtector Create(string masterKey = KhoaGoc)
        => new(Options.Create(new SecretProtectionOptions { MasterKey = masterKey }),
            NullLogger<AesGcmSecretProtector>.Instance);

    [Fact]
    public void Protect_RoiUnprotect_TraVeDungGiaTriBanDau()
    {
        var protector = Create();
        const string apiKey = "sk-ant-api03-vi-du-khoa-that-se-dai-hon-nhieu";

        var maHoa = protector.Protect(apiKey);

        Assert.NotEqual(apiKey, maHoa);
        Assert.Equal(apiKey, protector.Unprotect(maHoa));
    }

    [Fact]
    public void Protect_CungGiaTriVanRaHaiChuoiKhacNhau()
    {
        var protector = Create();

        var a = protector.Protect("cung-mot-bi-mat");
        var b = protector.Protect("cung-mot-bi-mat");

        // Nonce ngẫu nhiên mỗi lần: nhìn hai dòng trong DB không suy ra được
        // chúng đang giữ cùng một giá trị.
        Assert.NotEqual(a, b);
        Assert.Equal("cung-mot-bi-mat", protector.Unprotect(a));
        Assert.Equal("cung-mot-bi-mat", protector.Unprotect(b));
    }

    [Fact]
    public void Unprotect_KhiKhoaGocDaDoi_TraVeNullChuKhongNemLoi()
    {
        var maHoa = Create().Protect("khoa-api-cua-nha-cung-cap");

        var protectorKhoaKhac = Create("mot-khoa-hoan-toan-khac-cung-du-32-ky-tu-000");

        // Đây là kịch bản thật hay gặp nhất: đổi APP_MASTER_KEY sau khi deploy.
        // Phải hỏng êm để app vẫn chạy và coi như chưa cấu hình, không được sập.
        Assert.Null(protectorKhoaKhac.Unprotect(maHoa));
    }

    [Theory]
    [InlineData("")]
    [InlineData("khong-dung-dinh-dang")]
    [InlineData("v9.aaa.bbb.ccc")]
    [InlineData("v1.khong-phai-base64.x.y")]
    public void Unprotect_DuLieuHong_TraVeNull(string hong)
    {
        Assert.Null(Create().Unprotect(hong));
    }

    [Fact]
    public void Protect_CoTienToPhienBanDeSauNayDoiThuatToanVanGiaiMaDuocBanCu()
    {
        Assert.StartsWith("v1.", Create().Protect("bat-ky"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("ngan", "****")]
    [InlineData("sk-ant-api03-abcdefghijklmnop", "sk-a...mnop")]
    public void Mask_KhongBaoGioLoPhanGiua(string dauVao, string mongDoi)
    {
        Assert.Equal(mongDoi, Create().Mask(dauVao));
    }

    [Theory]
    [InlineData("")]
    [InlineData("qua-ngan")]
    public void KhoiTao_KhoaGocQuaNganThiNemLoiNgayLucKhoiDong(string khoaXau)
    {
        // Thà không khởi động được còn hơn chạy với mã hoá yếu mà không ai biết.
        var ex = Assert.Throws<InvalidOperationException>(() => Create(khoaXau));
        Assert.Contains("32", ex.Message, StringComparison.Ordinal);
    }
}

public class TokenServiceTests
{
    [Fact]
    public void GenerateToken_MoiLanMotGiaTriKhacNhau()
    {
        var service = new TokenService();

        var tokens = Enumerable.Range(0, 200).Select(_ => service.GenerateToken()).ToHashSet();

        Assert.Equal(200, tokens.Count);
    }

    [Fact]
    public void GenerateToken_AnToanKhiDatVaoUrlVaCookie()
    {
        var token = new TokenService().GenerateToken();

        // base64url: không có +, / hay = nên nhét vào URL hoặc cookie đều không phải escape.
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void HashToken_LaHamThuanVaTraVeSha256Hex()
    {
        var service = new TokenService();

        var hash = service.HashToken("mot-token-bat-ky");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, service.HashToken("mot-token-bat-ky"));
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void HashToken_HaiTokenKhacNhauChoHaiHashKhacNhau()
    {
        var service = new TokenService();

        Assert.NotEqual(service.HashToken("token-a"), service.HashToken("token-b"));
    }

    [Theory]
    [InlineData("giong-nhau", "giong-nhau", true)]
    [InlineData("khac-nhau", "khac-nhau-hon", false)]
    [InlineData("cung-do-dai-a", "cung-do-dai-b", false)]
    [InlineData("", "", true)]
    public void FixedTimeEquals_SoSanhDung(string a, string b, bool mongDoi)
    {
        Assert.Equal(mongDoi, new TokenService().FixedTimeEquals(a, b));
    }
}
