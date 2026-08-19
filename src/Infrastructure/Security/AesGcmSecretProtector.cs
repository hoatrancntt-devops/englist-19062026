using System.Security.Cryptography;
using System.Text;
using EnglishForIT.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Security;

public class SecretProtectionOptions
{
    /// <summary>
    /// Bí mật ứng dụng, tối thiểu 32 ký tự. Khoá mã hoá dẫn xuất từ đây bằng HKDF.
    ///
    /// ĐỔI GIÁ TRỊ NÀY = MẤT TOÀN BỘ BÍ MẬT ĐÃ LƯU TRONG DB. App sẽ coi như chưa cấu hình
    /// mail và AI, và sẽ không báo lỗi ồn ào — nên đây là hằng số phải giữ nguyên khi deploy.
    /// </summary>
    public string MasterKey { get; set; } = string.Empty;
}

/// <summary>
/// Mã hoá đối xứng AES-256-GCM. Định dạng lưu: v1.&lt;nonce-b64&gt;.&lt;tag-b64&gt;.&lt;ciphertext-b64&gt;
///
/// Có tiền tố phiên bản để sau này đổi thuật toán mà vẫn giải mã được dữ liệu cũ.
/// </summary>
public class AesGcmSecretProtector : ISecretProtector
{
    private const string Version = "v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;
    private readonly ILogger<AesGcmSecretProtector> _logger;

    public AesGcmSecretProtector(IOptions<SecretProtectionOptions> options, ILogger<AesGcmSecretProtector> logger)
    {
        _logger = logger;

        var master = options.Value.MasterKey;
        if (string.IsNullOrWhiteSpace(master) || master.Length < 32)
        {
            throw new InvalidOperationException(
                "APP_MASTER_KEY chưa đặt hoặc ngắn hơn 32 ký tự. Sinh bằng: openssl rand -base64 48");
        }

        // HKDF thay vì dùng thẳng chuỗi làm khoá: chuỗi người đặt không có phân bố đều.
        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: Encoding.UTF8.GetBytes(master),
            outputLength: 32,
            salt: Encoding.UTF8.GetBytes("englishforit.secret.v1"),
            info: Encoding.UTF8.GetBytes("secret-protector"));
    }

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        return $"{Version}.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(tag)}.{Convert.ToBase64String(cipher)}";
    }

    public string? Unprotect(string ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            return null;
        }

        var parts = ciphertext.Split('.');
        if (parts.Length != 4 || parts[0] != Version)
        {
            // Không log nội dung — chỉ log việc giải mã hỏng.
            _logger.LogWarning("Không giải mã được một giá trị bí mật: định dạng không hợp lệ");
            return null;
        }

        try
        {
            var nonce = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);
            var cipher = Convert.FromBase64String(parts[3]);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            // Trường hợp thường gặp nhất: APP_MASTER_KEY đã bị đổi sau khi deploy.
            _logger.LogWarning(
                "Không giải mã được một giá trị bí mật. Nguyên nhân hay gặp: APP_MASTER_KEY đã đổi so với lúc ghi");
            return null;
        }
    }

    public string Mask(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        if (plaintext.Length <= 8)
        {
            return new string('*', plaintext.Length);
        }

        return $"{plaintext[..4]}...{plaintext[^4..]}";
    }
}
