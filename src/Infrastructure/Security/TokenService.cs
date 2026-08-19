using System.Security.Cryptography;
using System.Text;
using EnglishForIT.Application.Abstractions;

namespace EnglishForIT.Infrastructure.Security;

/// <summary>
/// Token phiên và token một lần. Nguyên tắc: giá trị thô chỉ tồn tại trong cookie hoặc email,
/// DB chỉ giữ SHA-256. Lộ bản dump DB không đủ để mạo danh ai.
///
/// Dùng SHA-256 trần chứ không dùng Argon2 ở đây là có chủ đích: token do máy sinh, 256 bit
/// entropy, không có gì để dò từ điển. Băm chậm chỉ làm mỗi request đăng nhập chậm thêm.
/// </summary>
public class TokenService : ITokenService
{
    public string GenerateToken(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base64UrlEncode(bytes);
    }

    public string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash);
    }

    public bool FixedTimeEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);

        // FixedTimeEquals đòi hai mảng cùng độ dài; so độ dài trước vẫn an toàn vì
        // độ dài của token không phải bí mật.
        return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Đồng hồ thật. Test thay bằng cài đặt giả.</summary>
public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
