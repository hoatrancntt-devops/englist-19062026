using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using EnglishForIT.Domain.Entities.Ops;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.IntegrationTests;

/// <summary>
/// Khu quản trị.
///
/// Kiểm ba bất biến, cả ba đều thuộc loại hỏng trong im lặng — không có test thì lần sửa
/// sau làm hỏng mà không ai biết cho tới khi có sự cố thật:
///
/// một, học viên thường không mở được endpoint quản trị;
/// hai, mật khẩu SMTP đi vào thì không đi ra, và được mã hoá khi lưu;
/// ba, ô mật khẩu để trống nghĩa là GIỮ NGUYÊN chứ không phải xoá.
/// </summary>
[Collection(ApiCollection.Name)]
public class AdminEndpointsTests(ApiFactory api)
{
    private const string Password = "mat-khau-smtp-rat-dai-2026";

    [Theory]
    [InlineData("/api/v1/admin/overview")]
    [InlineData("/api/v1/admin/mail")]
    [InlineData("/api/v1/admin/audit")]
    public async Task HocVienThuongKhongMoDuocKhuQuanTri(string path)
    {
        var learner = await api.NewLearnerAsync();

        var response = await learner.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MatKhauSmtpKhongBaoGioDiRaKhoiMayChu()
    {
        var admin = await api.NewAdminAsync();

        await SaveMailAsync(admin, port: 587, password: Password);

        var response = await admin.GetAsync("/api/v1/admin/mail");
        var raw = await response.Content.ReadAsStringAsync();

        // Không trả nguyên văn, và cũng không trả dạng che một phần — che một phần vẫn là
        // rò một phần, mà lại tạo cảm giác an toàn giả.
        Assert.DoesNotContain(Password, raw);
        Assert.DoesNotContain("smtpPassword", raw, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(raw);
        Assert.True(document.RootElement.GetProperty("hasPassword").GetBoolean());
    }

    [Fact]
    public async Task MatKhauSmtpDuocMaHoaKhiLuu()
    {
        var admin = await api.NewAdminAsync();

        await SaveMailAsync(admin, port: 587, password: Password);

        await using var scope = api.NewScope();
        var stored = await ApiFactory.Db(scope).MailSettings.AsNoTracking().FirstAsync();

        Assert.NotNull(stored.SmtpPasswordEncrypted);
        Assert.DoesNotContain(Password, stored.SmtpPasswordEncrypted);
    }

    [Fact]
    public async Task BoTrongOMatKhauThiGiuNguyenChuKhongXoa()
    {
        var admin = await api.NewAdminAsync();

        await SaveMailAsync(admin, port: 587, password: Password);

        // Sửa mỗi cổng, để trống ô mật khẩu — đúng thao tác người vận hành làm thường xuyên.
        await SaveMailAsync(admin, port: 2525, password: null);

        var response = await admin.GetAsync("/api/v1/admin/mail");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(2525, document.RootElement.GetProperty("smtpPort").GetInt32());

        // Nếu bất biến này hỏng, người vận hành mất mật khẩu mỗi lần sửa cổng mà không có
        // gì báo, và thư lặng lẽ ngừng gửi.
        Assert.True(document.RootElement.GetProperty("hasPassword").GetBoolean());
    }

    [Fact]
    public async Task NhatKyKiemToanGhiTenViecChuKhongGhiGiaTri()
    {
        var admin = await api.NewAdminAsync();

        await SaveMailAsync(admin, port: 587, password: Password);

        var response = await admin.GetAsync("/api/v1/admin/audit");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Contains("mail.settings_updated", raw);

        // Nhật ký mà chứa mật khẩu thì chính nó thành chỗ rò rỉ.
        Assert.DoesNotContain(Password, raw);
    }

    [Fact]
    public async Task TongQuanBaoDuocBaiKhongCoCauHoiNao()
    {
        var admin = await api.NewAdminAsync();

        var response = await admin.GetAsync("/api/v1/admin/overview");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var content = document.RootElement.GetProperty("content");

        // Nội dung đã qua cổng chất lượng nên phải sạch. Test này bắt lỗi theo chiều ngược lại:
        // nếu con số vọt lên, hoặc là nội dung hỏng, hoặc là chính phép đếm sai.
        Assert.Equal(0, content.GetProperty("lessonsWithoutItems").GetInt32());
        Assert.Equal(0, content.GetProperty("orphanReviewItems").GetInt32());
        Assert.True(content.GetProperty("totalAnswerableItems").GetInt32() > 0);
    }

    private static async Task SaveMailAsync(HttpClient admin, int port, string? password)
    {
        var response = await admin.PutAsJsonAsync("/api/v1/admin/mail", new
        {
            enabled = true,
            fromAddress = "khong-tra-loi@vidu.vn",
            fromDisplayName = "English for IT",
            smtpHost = "mailpit",
            smtpPort = port,
            smtpUseStartTls = false,
            smtpUsername = "nguoi-dung",
            smtpPassword = password,
        });

        response.EnsureSuccessStatusCode();
    }
}
