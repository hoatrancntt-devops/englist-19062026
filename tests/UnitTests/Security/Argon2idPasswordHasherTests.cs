using EnglishForIT.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace EnglishForIT.UnitTests.Security;

public class Argon2idPasswordHasherTests
{
    // Tham số hạ thấp để test chạy nhanh. Giá trị thật nằm trong appsettings.
    private static Argon2idPasswordHasher CreateHasher(int memoryKb = 1024, int iterations = 1)
        => new(Options.Create(new PasswordHashingOptions
        {
            MemorySizeKb = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = 1
        }));

    [Fact]
    public void Hash_TraVeDinhDangPhcCoThamSo()
    {
        var hash = CreateHasher().Hash("mat-khau-dai-va-de-nho");

        Assert.StartsWith("$argon2id$v=19$", hash, StringComparison.Ordinal);
        Assert.Contains("m=1024,t=1,p=1", hash, StringComparison.Ordinal);
        // argon2id | v=19 | m,t,p | salt | hash
        Assert.Equal(5, hash.Split('$', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Hash_CungMatKhauVanRaHaiChuoiKhacNhau()
    {
        var hasher = CreateHasher();

        var a = hasher.Hash("cung-mot-mat-khau");
        var b = hasher.Hash("cung-mot-mat-khau");

        // Salt ngẫu nhiên cho mỗi lần băm: hai người dùng đặt trùng mật khẩu
        // không được để lộ điều đó qua bảng users.
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Verify_DungMatKhauThiTra_True()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("mat-khau-dai-va-de-nho");

        Assert.True(hasher.Verify("mat-khau-dai-va-de-nho", hash));
    }

    [Fact]
    public void Verify_SaiMatKhauThiTra_False()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("mat-khau-dai-va-de-nho");

        Assert.False(hasher.Verify("mat-khau-dai-va-de-nh0", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("khong-phai-argon2")]
    [InlineData("$argon2id$v=19$thieu-phan-con-lai")]
    [InlineData("$argon2id$v=19$m=x,t=y,p=z$aaa$bbb")]
    public void Verify_HashHongThiTraFalseChuKhongNemLoi(string hongHash)
    {
        // Hash hỏng phải xử lý như sai mật khẩu. Ném lỗi ở đây sẽ thành 500,
        // và một request 500 giữa một loạt 401 chính là tín hiệu cho kẻ dò tài khoản.
        Assert.False(CreateHasher().Verify("bat-ky-mat-khau-nao", hongHash));
    }

    [Fact]
    public void NeedsRehash_ThamSoCuThapHonCauHinhHienTai_TraVeTrue()
    {
        var hashCu = CreateHasher(memoryKb: 1024, iterations: 1).Hash("mat-khau-dai-va-de-nho");

        var hasherMoi = CreateHasher(memoryKb: 4096, iterations: 3);

        Assert.True(hasherMoi.NeedsRehash(hashCu));

        // Nâng tham số không được làm hỏng hash cũ: người dùng vẫn phải đăng nhập được.
        Assert.True(hasherMoi.Verify("mat-khau-dai-va-de-nho", hashCu));
    }

    [Fact]
    public void NeedsRehash_ThamSoDungCauHinh_TraVeFalse()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("mat-khau-dai-va-de-nho");

        Assert.False(hasher.NeedsRehash(hash));
    }
}
