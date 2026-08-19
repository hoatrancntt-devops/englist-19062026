using System.Text.Json;
using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Learning;

public record SpeechGrade(
    bool Graded,
    string? TranscriptEn,
    double PronunciationScore,
    double FluencyScore,
    double CommunicationScore,
    double OverallScore,
    IReadOnlyList<string> MissedWords,
    IReadOnlyList<string> FeedbackVi,
    string? UnavailableReasonVi);

/// <summary>
/// Chấm một lượt nói.
///
/// Nhận dạng chạy tại chỗ trong container riêng — <b>giọng của học viên không rời máy chủ</b>.
/// Đây là ràng buộc riêng tư đã chốt, không phải lựa chọn kỹ thuật, và nó là lý do không dùng
/// dịch vụ nhận dạng của bên thứ ba dù rẻ hơn.
///
/// Dịch vụ nhận dạng chết thì trả về chưa-chấm-được kèm lý do, KHÔNG bịa điểm. Cho 0 điểm
/// khi máy hỏng là đổ lỗi máy móc lên đầu học viên; cho 100 điểm là nói dối họ đã thạo.
/// </summary>
public class SpeechService(
    AppDbContext db,
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    ILogger<SpeechService> logger)
{
    private readonly string _baseUrl = configuration.GetValue<string>("Speech:BaseUrl") ?? "http://speech:9000/";
    private readonly string _audioRoot = configuration.GetValue<string>("Storage:AudioRoot") ?? "media";
    private readonly bool _enabled = configuration.GetValue("Speech:Enabled", false);

    /// <summary>Trọng số ba trục. Truyền đạt nặng nhất: nói sai vài âm còn đỡ hơn nói thiếu ý.</summary>
    private const double PronunciationWeight = 0.35;
    private const double FluencyWeight = 0.20;
    private const double CommunicationWeight = 0.45;

    public bool Enabled => _enabled;

    public async Task<SpeechGrade> GradeAsync(
        Guid userId,
        string contextType,
        Guid? contextId,
        string expectedText,
        // Mảng byte chứ không phải Stream: MultipartFormDataContent ĐÓNG stream bên trong nó
        // sau khi gửi, nên bước lưu file sau đó sẽ gặp stream đã đóng.
        byte[] audio,
        string fileName,
        int durationMs,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (!_enabled)
        {
            return Unavailable("Phần chấm phát âm chưa được bật trên máy chủ này.");
        }

        string? transcript;

        try
        {
            transcript = await TranscribeAsync(audio, fileName, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Dịch vụ nhận dạng giọng nói không phản hồi");
            return Unavailable("Dịch vụ chấm phát âm đang không phản hồi. Bước này chưa được tính điểm.");
        }

        // Không nghe được gì gần như luôn là micro tắt, chọn nhầm thiết bị, hoặc phòng quá ồn —
        // chứ không phải học viên nói sai. Lưu 0 điểm ở đây là đổ lỗi máy móc lên đầu họ, và
        // còn làm hỏng dữ liệu dùng để hiệu chỉnh ngưỡng chấm về sau.
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return Unavailable(
                "Không nghe thấy gì nên chưa chấm được. Kiểm tra micro rồi thử lại — "
                + "nói cách micro khoảng một gang tay.");
        }

        var score = SpeechScorer.Score(expectedText, transcript, durationMs);

        var overall = Math.Round(
            score.PronunciationScore * PronunciationWeight
            + score.FluencyScore * FluencyWeight
            + score.CommunicationScore * CommunicationWeight, 1);

        // Lưu file để hiệu chỉnh ngưỡng chấm về sau. Job dọn dẹp xoá sau 45 ngày,
        // chỉ xoá file và giữ lại dòng điểm.
        var relativePath = await SaveAudioAsync(userId, audio, fileName, now, ct);

        db.SpeechAttempts.Add(new SpeechAttempt
        {
            UserId = userId,
            ContextType = contextType,
            ContextId = contextId,
            ExpectedText = expectedText,
            TranscriptEn = transcript,
            PronunciationScore = score.PronunciationScore,
            FluencyScore = score.FluencyScore,
            CommunicationScore = score.CommunicationScore,
            FeedbackViJson = JsonSerializer.Serialize(score.FeedbackVi),
            AudioRelativePath = relativePath,
            DurationMs = durationMs,
        });

        await db.SaveChangesAsync(ct);

        return new SpeechGrade(
            true,
            transcript,
            score.PronunciationScore,
            score.FluencyScore,
            score.CommunicationScore,
            overall,
            score.MissedWords,
            score.FeedbackVi,
            null);
    }

    private async Task<string?> TranscribeAsync(byte[] audio, string fileName, CancellationToken ct)
    {
        var client = httpFactory.CreateClient(nameof(SpeechService));
        client.BaseAddress = new Uri(_baseUrl);

        // Nhận dạng chậm hơn hẳn mọi lời gọi khác trong hệ thống nên timeout riêng.
        client.Timeout = TimeSpan.FromSeconds(60);

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(audio), "audio_file", fileName },
        };

        var response = await client.PostAsync("asr?encode=true&task=transcribe&output=json", content, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        return doc.RootElement.TryGetProperty("text", out var text) ? text.GetString()?.Trim() : null;
    }

    /// <summary>
    /// Ghi file vào thư mục theo ngày.
    ///
    /// Chia theo ngày để job dọn dẹp không phải quét một thư mục có hàng trăm nghìn file,
    /// và để xoá theo lô nhanh hơn.
    /// </summary>
    private async Task<string?> SaveAudioAsync(
        Guid userId, byte[] audio, string fileName, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            var relativeDir = Path.Combine("speech", now.ToString("yyyy-MM-dd"));
            var absoluteDir = Path.Combine(_audioRoot, relativeDir);

            Directory.CreateDirectory(absoluteDir);

            var extension = Path.GetExtension(fileName);
            var name = $"{userId:N}-{Guid.CreateVersion7():N}{(string.IsNullOrEmpty(extension) ? ".webm" : extension)}";

            await File.WriteAllBytesAsync(Path.Combine(absoluteDir, name), audio, ct);

            return Path.Combine(relativeDir, name).Replace('\\', '/');
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Không ghi được file thì vẫn giữ điểm: điểm mới là thứ học viên cần,
            // file chỉ để hiệu chỉnh ngưỡng về sau.
            logger.LogWarning(ex, "Không lưu được file ghi âm, vẫn giữ bản ghi điểm");
            return null;
        }
    }

    private static SpeechGrade Unavailable(string reason) =>
        new(false, null, 0, 0, 0, 0, [], [], reason);
}
