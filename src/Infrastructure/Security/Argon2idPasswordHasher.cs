using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EnglishForIT.Application.Abstractions;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Security;

/// <summary>
/// Tham số Argon2id. Mặc định hạ bộ nhớ xuống 32 MB vì máy chủ còn phải chừa RAM cho
/// dịch vụ nhận dạng giọng nói. Đây là đánh đổi có chủ đích, ghi rõ trong ARCHITECTURE.md.
/// </summary>
public class PasswordHashingOptions
{
    public int MemorySizeKb { get; set; } = 32 * 1024;
    public int Iterations { get; set; } = 3;
    public int DegreeOfParallelism { get; set; } = 2;
    public int SaltLength { get; set; } = 16;
    public int HashLength { get; set; } = 32;
}

/// <summary>
/// Băm mật khẩu bằng Argon2id, đóng gói tham số vào chuỗi kết quả theo định dạng PHC:
/// $argon2id$v=19$m=32768,t=3,p=2$&lt;salt-b64&gt;$&lt;hash-b64&gt;
///
/// Đóng gói tham số nghĩa là nâng tham số sau này không làm hỏng hash cũ.
/// </summary>
public class Argon2idPasswordHasher(IOptions<PasswordHashingOptions> options) : IPasswordHasher
{
    private const string Prefix = "$argon2id$v=19$";
    private readonly PasswordHashingOptions _o = options.Value;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_o.SaltLength);
        var hash = Derive(password, salt, _o.MemorySizeKb, _o.Iterations, _o.DegreeOfParallelism, _o.HashLength);

        return string.Create(CultureInfo.InvariantCulture,
            $"{Prefix}m={_o.MemorySizeKb},t={_o.Iterations},p={_o.DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public bool Verify(string password, string encodedHash)
    {
        // Email không tồn tại thì tầng trên vẫn gọi vào đây với hash giả, để tổng thời gian
        // của hai nhánh gần bằng nhau. Vì vậy phân tích hỏng cũng phải tốn công tương đương.
        if (!TryParse(encodedHash, out var p))
        {
            _ = Derive(password, RandomNumberGenerator.GetBytes(_o.SaltLength),
                _o.MemorySizeKb, _o.Iterations, _o.DegreeOfParallelism, _o.HashLength);
            return false;
        }

        var computed = Derive(password, p.Salt, p.Memory, p.Iterations, p.Parallelism, p.Hash.Length);
        return CryptographicOperations.FixedTimeEquals(computed, p.Hash);
    }

    public bool NeedsRehash(string encodedHash)
    {
        if (!TryParse(encodedHash, out var p))
        {
            return true;
        }

        return p.Memory < _o.MemorySizeKb
               || p.Iterations < _o.Iterations
               || p.Parallelism != _o.DegreeOfParallelism;
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int length)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon.GetBytes(length);
    }

    private static bool TryParse(string encoded, out ParsedHash parsed)
    {
        parsed = default;

        if (string.IsNullOrEmpty(encoded) || !encoded.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = encoded[Prefix.Length..].Split('$');
        if (parts.Length != 3)
        {
            return false;
        }

        int memory = 0, iterations = 0, parallelism = 0;

        foreach (var segment in parts[0].Split(','))
        {
            var kv = segment.Split('=');
            if (kv.Length != 2 || !int.TryParse(kv[1], CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            switch (kv[0])
            {
                case "m": memory = value; break;
                case "t": iterations = value; break;
                case "p": parallelism = value; break;
            }
        }

        if (memory <= 0 || iterations <= 0 || parallelism <= 0)
        {
            return false;
        }

        try
        {
            parsed = new ParsedHash(memory, iterations, parallelism,
                Convert.FromBase64String(parts[1]), Convert.FromBase64String(parts[2]));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private readonly record struct ParsedHash(
        int Memory, int Iterations, int Parallelism, byte[] Salt, byte[] Hash);
}
