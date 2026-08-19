using System.Security.Claims;
using System.Text.Json;
using EnglishForIT.Application.Abstractions;
using EnglishForIT.Domain.Entities.Ops;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Content;
using EnglishForIT.Infrastructure.Ops;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.Api.Modules;

public record AdminOverview(
    int Learners,
    int LessonsPublished,
    int PlacementForms,
    int RoleplayScenarios,
    int LessonItems,
    int ActiveSessions,
    OutboxOverview Outbox,
    ContentHealth Content);

public record OutboxOverview(int Pending, int Sent, int Failed, string? LastError);

/// <summary>
/// Sức khoẻ nội dung: những thứ hỏng âm thầm mà không có màn nào khác cho thấy.
/// </summary>
public record ContentHealth(
    int LessonsWithoutItems,
    int OrphanReviewItems,
    int MostCommonAnswerPosition,
    int MostCommonAnswerCount,
    int TotalAnswerableItems);

public record MailSettingsView(
    bool Enabled,
    string Provider,
    string FromAddress,
    string FromDisplayName,
    string? SmtpHost,
    int? SmtpPort,
    bool SmtpUseStartTls,
    string? SmtpUsername,
    /// <summary>Chỉ cho biết CÓ mật khẩu hay không. Không bao giờ trả giá trị, kể cả dạng che.</summary>
    bool HasPassword,
    DateTimeOffset? LastTestAt,
    bool? LastTestSucceeded,
    string? LastTestError);

public record MailSettingsUpdate(
    bool Enabled,
    string FromAddress,
    string FromDisplayName,
    string? SmtpHost,
    int? SmtpPort,
    bool SmtpUseStartTls,
    string? SmtpUsername,
    /// <summary>Bỏ trống nghĩa là giữ nguyên mật khẩu cũ, không phải xoá nó.</summary>
    string? SmtpPassword);

public record SendTestEmailRequest(string ToAddress);

/// <summary>
/// Khu quản trị.
///
/// Hai nguyên tắc chi phối mọi endpoint ở đây:
///
/// Một, bí mật đi vào thì không đi ra. Mật khẩu SMTP và khoá API nhận được thì mã hoá và
/// chỉ trả về cờ có/không — không trả dạng che một phần, vì che một phần vẫn là rò một phần.
///
/// Hai, mọi thay đổi cấu hình đều ghi nhật ký kiểm toán, và nhật ký đó chỉ ghi TÊN hành động
/// cùng đối tượng, tuyệt đối không ghi giá trị.
/// </summary>
public static class AdminModule
{
    public static IEndpointRouteBuilder MapAdminModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin").WithTags("Admin");

        group.MapGet("/overview", Overview)
            .WithSummary("Số liệu tổng quan và sức khoẻ nội dung");

        group.MapGet("/mail", GetMail)
            .WithSummary("Cấu hình gửi thư. Không bao giờ trả mật khẩu.");

        group.MapPut("/mail", UpdateMail)
            .WithSummary("Cập nhật cấu hình gửi thư. Bỏ trống mật khẩu thì giữ nguyên giá trị cũ.");

        group.MapPost("/mail/test", SendTestEmail)
            .WithSummary("Xếp một thư thử vào hộp thư đi để xác nhận cấu hình chạy được");

        group.MapPost("/content/reseed", Reseed)
            .WithSummary("Nạp lại nội dung từ YAML mà không cần khởi động lại API");

        group.MapGet("/audit", Audit)
            .WithSummary("Nhật ký kiểm toán, mới nhất trước");

        return app;
    }

    private static async Task<IResult> Overview(
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!AdminAccess.IsAdmin(principal))
        {
            return AdminAccess.Denied();
        }

        var now = DateTimeOffset.UtcNow;

        // Bài không có câu hỏi nào là hỏng âm thầm: học viên mở ra thấy màn trống
        // và không có lỗi nào được ghi ở đâu.
        var lessonsWithoutItems = await db.Lessons
            .Where(l => l.Status == ContentStatus.Published)
            .CountAsync(l => !db.LessonActivities.Any(a => a.LessonId == l.Id && a.Items.Count > 0), ct);

        var orphanReviews = await db.ReviewQueue
            .CountAsync(r => !db.LessonItems.Any(i => i.Id == r.ItemId), ct);

        // Vị trí đáp án dồn về một chỗ: học viên chọn mãi ô đó là qua bài mà không cần đọc.
        var answers = await db.LessonItems.Select(i => i.AnswerJson).ToListAsync(ct);

        var positions = answers
            .Select(TryReadAnswer)
            .Where(p => p >= 0)
            .ToList();

        var mostCommon = positions.Count > 0
            ? positions.GroupBy(p => p).OrderByDescending(g => g.Count()).First()
            : null;

        var outbox = await db.OutboxEmails
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var lastError = await db.OutboxEmails
            .Where(e => e.LastError != null)
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => e.LastError)
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new AdminOverview(
            await db.Users.CountAsync(ct),
            await db.Lessons.CountAsync(l => l.Status == ContentStatus.Published, ct),
            await db.PlacementForms.CountAsync(ct),
            await db.RoleplayScenarios.CountAsync(s => s.Status == ContentStatus.Published, ct),
            await db.LessonItems.CountAsync(ct),
            await db.Sessions.CountAsync(s => s.ExpiresAt > now && s.RevokedAt == null, ct),
            new OutboxOverview(
                outbox.FirstOrDefault(o => o.Status == OutboxStatus.Pending)?.Count ?? 0,
                outbox.FirstOrDefault(o => o.Status == OutboxStatus.Sent)?.Count ?? 0,
                outbox.FirstOrDefault(o => o.Status == OutboxStatus.Failed)?.Count ?? 0,
                lastError),
            new ContentHealth(
                lessonsWithoutItems,
                orphanReviews,
                mostCommon?.Key ?? -1,
                mostCommon?.Count() ?? 0,
                positions.Count)));
    }

    private static async Task<IResult> GetMail(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!AdminAccess.IsAdmin(principal))
        {
            return AdminAccess.Denied();
        }

        var settings = await db.MailSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        if (settings is null)
        {
            return Results.Ok(new MailSettingsView(
                false, nameof(MailProvider.Smtp), "", "", null, null, true, null, false, null, null, null));
        }

        return Results.Ok(new MailSettingsView(
            settings.Enabled,
            settings.Provider.ToString(),
            settings.FromAddress,
            settings.FromDisplayName,
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SmtpUseStartTls,
            settings.SmtpUsername,
            !string.IsNullOrWhiteSpace(settings.SmtpPasswordEncrypted),
            settings.LastTestAt,
            settings.LastTestSucceeded,
            settings.LastTestError));
    }

    private static async Task<IResult> UpdateMail(
        [FromBody] MailSettingsUpdate update,
        ClaimsPrincipal principal,
        AppDbContext db,
        ISecretProtector secrets,
        CancellationToken ct)
    {
        if (!AdminAccess.IsAdmin(principal))
        {
            return AdminAccess.Denied();
        }

        var settings = await db.MailSettings.FirstOrDefaultAsync(ct);

        if (settings is null)
        {
            settings = new MailSetting
            {
                FromAddress = update.FromAddress,
                FromDisplayName = update.FromDisplayName,
            };

            db.MailSettings.Add(settings);
        }

        settings.Enabled = update.Enabled;
        settings.Provider = MailProvider.Smtp;
        settings.FromAddress = update.FromAddress;
        settings.FromDisplayName = update.FromDisplayName;
        settings.SmtpHost = update.SmtpHost;
        settings.SmtpPort = update.SmtpPort;
        settings.SmtpUseStartTls = update.SmtpUseStartTls;
        settings.SmtpUsername = update.SmtpUsername;

        // Bỏ trống là GIỮ NGUYÊN, không phải xoá. Màn quản trị không hiển thị được mật khẩu
        // cũ nên nếu bỏ trống mà xoá thì mỗi lần sửa cổng SMTP lại mất mật khẩu.
        if (!string.IsNullOrWhiteSpace(update.SmtpPassword))
        {
            settings.SmtpPasswordEncrypted = secrets.Protect(update.SmtpPassword);
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = AdminAccess.UserId(principal),
            Action = "mail.settings_updated",
            TargetType = "mail_settings",
            TargetId = settings.Id.ToString(),

            // Ghi CÁI GÌ đổi, không ghi ĐỔI THÀNH GÌ.
            MetadataJson = JsonSerializer.Serialize(new
            {
                enabled = update.Enabled,
                host = update.SmtpHost,
                port = update.SmtpPort,
                passwordChanged = !string.IsNullOrWhiteSpace(update.SmtpPassword),
            }),
        });

        await db.SaveChangesAsync(ct);

        return Results.Ok(new { message = "Đã lưu cấu hình gửi thư." });
    }

    private static async Task<IResult> SendTestEmail(
        [FromBody] SendTestEmailRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        if (!AdminAccess.IsAdmin(principal))
        {
            return AdminAccess.Denied();
        }

        // Khoá chống gửi trùng có mốc thời gian: bấm thử nhiều lần là chuyện bình thường
        // khi đang dò cấu hình, và mỗi lần phải là một thư thật.
        var key = $"admin_test:{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        db.OutboxEmails.Add(new OutboxEmail
        {
            ToAddress = request.ToAddress,
            Subject = "Thư thử từ English for IT",
            HtmlBody = "<p>Nếu bạn đọc được thư này thì cấu hình gửi thư đã chạy đúng.</p>",
            TextBody = "Neu ban doc duoc thu nay thi cau hinh gui thu da chay dung.",
            IdempotencyKey = key,
            NextAttemptAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            message = "Đã xếp thư thử vào hộp thư đi. Worker gửi trong vòng một phút.",
        });
    }

    private static async Task<IResult> Reseed(
        ClaimsPrincipal principal,
        IConfiguration config,
        ContentSeeder content,
        PlacementSeeder placement,
        RoleplaySeeder roleplay,
        StorySeeder story,
        WritingSeeder writing,
        AppDbContext db,
        CancellationToken ct)
    {
        if (!AdminAccess.IsAdmin(principal))
        {
            return AdminAccess.Denied();
        }

        var root = config.GetValue<string>("Content:Root") ?? "content";

        // Mỗi loại nội dung nạp riêng: một loại hỏng không được ngăn các loại còn lại.
        // Truyện nạp sau bài học vì cổng chất lượng của nó tra mốc mở trên danh sách bài.
        var lessons = await content.SeedAsync(root, ct);
        var forms = await placement.SeedAsync(root, ct);
        var scenarios = await roleplay.SeedAsync(root, ct);
        var chapters = await story.SeedAsync(root, ct);
        var writingSets = await writing.SeedAsync(root, ct);

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = AdminAccess.UserId(principal),
            Action = "content.reseeded",
            TargetType = "content",
            MetadataJson = JsonSerializer.Serialize(new
            {
                lessons = lessons.ToString(),
                forms = forms.ToString(),
                scenarios = scenarios.ToString(),
                chapters = chapters.ToString(),
                writing = writingSets.ToString(),
            }),
        });

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            lessons = new { lessons.Inserted, lessons.Updated, lessons.Unchanged, problems = lessons.Problems },
            placement = new { forms.Inserted, forms.Updated, forms.Unchanged, problems = forms.Problems },
            roleplay = new { scenarios.Inserted, scenarios.Updated, scenarios.Unchanged, problems = scenarios.Problems },
            story = new { chapters.Inserted, chapters.Updated, chapters.Unchanged, problems = chapters.Problems },
            writing = new { writingSets.Inserted, writingSets.Updated, writingSets.Unchanged, problems = writingSets.Problems },
        });
    }

    private static async Task<IResult> Audit(
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!AdminAccess.IsAdmin(principal))
        {
            return AdminAccess.Denied();
        }

        var logs = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .Select(a => new { a.CreatedAt, a.Action, a.TargetType, a.TargetId, a.MetadataJson })
            .ToListAsync(ct);

        return Results.Ok(logs);
    }

    /// <summary>Đọc vị trí đáp án đúng, trả -1 nếu câu này không có đáp án dạng chỉ số.</summary>
    private static int TryReadAnswer(string answerJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(answerJson);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "answer", StringComparison.OrdinalIgnoreCase)
                    && property.Value.TryGetInt32(out var index))
                {
                    return index;
                }
            }
        }
        catch (JsonException)
        {
            // Câu hỏng thì bỏ qua khi thống kê, đã có cổng chất lượng lo việc chặn nó.
        }

        return -1;
    }

}
