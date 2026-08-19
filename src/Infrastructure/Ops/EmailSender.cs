using System.Net;
using System.Net.Mail;
using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using EnglishForIT.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Ops;

public record EmailSendResult(bool Success, string? Error);

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(OutboxEmail email, CancellationToken ct = default);
}

/// <summary>
/// Gửi thư qua SMTP.
///
/// Cấu hình đọc từ bảng <c>mail_settings</c> chứ không từ biến môi trường: quản trị viên
/// đổi được trên web mà không cần triển khai lại. Mật khẩu lưu đã mã hoá và chỉ giải mã
/// ngay tại đây, không bao giờ trả ra API.
///
/// Chưa bật hoặc chưa cấu hình thì trả thất bại có lý do rõ ràng, KHÔNG ném lỗi:
/// hộp thư đi sẽ thử lại, và một hệ thống chưa cấu hình mail không nên làm worker chết.
/// </summary>
public class SmtpEmailSender(
    AppDbContext db,
    ISecretProtector secrets,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(OutboxEmail email, CancellationToken ct = default)
    {
        var settings = await db.MailSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        if (settings is null || !settings.Enabled)
        {
            return new EmailSendResult(false, "Chưa bật gửi thư trong cài đặt.");
        }

        if (settings.Provider != MailProvider.Smtp)
        {
            // Microsoft Graph chưa nối. Nói thẳng thay vì im lặng bỏ qua thư.
            return new EmailSendResult(false, $"Nhà cung cấp {settings.Provider} chưa được hỗ trợ.");
        }

        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || settings.SmtpPort is null)
        {
            return new EmailSendResult(false, "Thiếu host hoặc port SMTP.");
        }

        try
        {
            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort.Value)
            {
                EnableSsl = settings.SmtpUseStartTls,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };

            if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
            {
                var password = string.IsNullOrWhiteSpace(settings.SmtpPasswordEncrypted)
                    ? string.Empty
                    : secrets.Unprotect(settings.SmtpPasswordEncrypted);

                client.Credentials = new NetworkCredential(settings.SmtpUsername, password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(settings.FromAddress, settings.FromDisplayName),
                Subject = email.Subject,
                Body = email.HtmlBody,
                IsBodyHtml = true,
            };

            message.To.Add(new MailAddress(email.ToAddress, email.ToDisplayName ?? email.ToAddress));

            if (!string.IsNullOrWhiteSpace(email.TextBody))
            {
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                    email.TextBody, null, "text/plain"));
            }

            await client.SendMailAsync(message, ct);

            return new EmailSendResult(true, null);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            logger.LogWarning(ex, "Gửi thư tới {To} thất bại", email.ToAddress);

            // Chỉ ghi thông điệp, KHÔNG ghi stack trace hay cấu hình: cột này hiện trên
            // màn quản trị và không được để lộ thứ gì giống bí mật.
            return new EmailSendResult(false, ex.Message);
        }
    }
}
