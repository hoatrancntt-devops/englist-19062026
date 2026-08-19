using EnglishForIT.Application.Learning;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Ops;

public record RetentionReport(int FilesDeleted, int RowsKept, int MissingFiles);

/// <summary>
/// Xoá file ghi âm quá hạn giữ.
///
/// Chỉ xoá FILE, giữ nguyên bản ghi điểm: điểm số vẫn dùng để vẽ tiến bộ sau khi âm thanh
/// đã bị xoá, còn giọng nói thì không có lý do gì phải giữ vô thời hạn. Mốc 45 ngày là
/// quyết định đã chốt, nằm ở <see cref="LearningPolicyOptions.SpeechAudioRetentionDays"/>.
///
/// Trước job này, mốc đó chỉ tồn tại trong tài liệu và file ghi âm tích luỹ mãi mãi.
/// </summary>
public class AudioRetentionJob(
    AppDbContext db,
    IOptions<LearningPolicyOptions> policyOptions,
    IConfiguration configuration,
    ILogger<AudioRetentionJob> logger)
{
    private readonly LearningPolicyOptions _policy = policyOptions.Value;
    private readonly string _storageRoot = configuration.GetValue<string>("Storage:AudioRoot") ?? "storage/audio";

    public async Task<RetentionReport> RunAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var cutoff = now.AddDays(-_policy.SpeechAudioRetentionDays);

        var expired = await db.SpeechAttempts
            .Where(a => a.CreatedAt < cutoff && a.AudioRelativePath != null)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return new RetentionReport(0, 0, 0);
        }

        int deleted = 0, missing = 0;

        foreach (var attempt in expired)
        {
            // Đường dẫn trong DB là tương đối; gốc lấy từ cấu hình để dev và production
            // không phải trỏ cùng một chỗ.
            var path = Path.Combine(_storageRoot, attempt.AudioRelativePath!);

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted++;
                }
                else
                {
                    missing++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Xoá không được thì để lần sau thử lại: KHÔNG gỡ đường dẫn khỏi DB,
                // vì làm vậy là mất dấu file và nó nằm lại trên đĩa vĩnh viễn.
                logger.LogWarning(ex, "Không xoá được file ghi âm {Path}", path);
                continue;
            }

            // Gỡ đường dẫn nhưng giữ nguyên dòng điểm.
            attempt.AudioRelativePath = null;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Dọn ghi âm quá {Days} ngày: xoá {Deleted} file, {Missing} file đã không còn, giữ {Rows} dòng điểm",
            _policy.SpeechAudioRetentionDays, deleted, missing, expired.Count);

        return new RetentionReport(deleted, expired.Count, missing);
    }
}
