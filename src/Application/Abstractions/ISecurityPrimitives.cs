namespace EnglishForIT.Application.Abstractions;

/// <summary>Băm và kiểm mật khẩu. Chỉ có một cài đặt: Argon2id.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Kiểm mật khẩu. Luôn chạy hết công việc băm kể cả khi hash đầu vào rỗng,
    /// để thời gian phản hồi không tiết lộ email có tồn tại hay không.
    /// </summary>
    bool Verify(string password, string encodedHash);

    /// <summary>Hash được tạo bằng tham số cũ hơn cấu hình hiện tại, nên băm lại khi đăng nhập.</summary>
    bool NeedsRehash(string encodedHash);
}

/// <summary>Sinh và băm token phiên, token đặt lại mật khẩu, token xác minh email.</summary>
public interface ITokenService
{
    /// <summary>Sinh token ngẫu nhiên an toàn mật mã, trả về chuỗi base64url.</summary>
    string GenerateToken(int byteLength = 32);

    /// <summary>SHA-256 hex thường. Token thô đi cho người dùng, hash này đi vào DB.</summary>
    string HashToken(string token);

    /// <summary>So sánh chuỗi theo thời gian hằng định.</summary>
    bool FixedTimeEquals(string a, string b);
}

/// <summary>
/// Mã hoá bí mật trước khi ghi DB: khoá API của nhà cung cấp AI, client secret của mail.
/// Khoá dẫn xuất từ bí mật ứng dụng — đổi bí mật đó nghĩa là mất toàn bộ giá trị đã mã hoá.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    /// <summary>Trả về null nếu không giải mã được, thay vì ném lỗi — cấu hình hỏng không được làm sập app.</summary>
    string? Unprotect(string ciphertext);

    /// <summary>Dạng che để hiển thị trên admin, ví dụ sk-ant-...4f2a.</summary>
    string Mask(string plaintext);
}

/// <summary>Đồng hồ hệ thống, tách ra để test kiểm được logic phụ thuộc thời gian.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
