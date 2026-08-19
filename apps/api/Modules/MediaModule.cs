using System.Security.Claims;
using EnglishForIT.Application.Content;
using EnglishForIT.Infrastructure.Content;
using Microsoft.Net.Http.Headers;

namespace EnglishForIT.Api.Modules;

/// <summary>
/// Phục vụ file audio sinh sẵn cho các nút bấm nghe.
///
/// Chỉ trả file ĐÃ CÓ, tuyệt đối không tổng hợp giọng theo yêu cầu. Nếu endpoint này sinh
/// audio cho bất kỳ chuỗi nào gửi lên thì bất kỳ ai cũng bắt được máy chủ đọc thuê hàng nghìn
/// đoạn văn của họ, và một máy dùng chung với hai stack khác sẽ chết vì CPU chứ không phải vì
/// lưu lượng.
///
/// Chưa có file thì trả 404 chứ không trả lỗi. Giao diện hiểu 404 là "chưa sinh xong" và tự
/// quay về giọng đọc của trình duyệt, nên học viên vẫn nghe được trong lúc mẻ sinh còn chạy.
/// </summary>
public static class MediaModule
{
    /// <summary>
    /// Một năm. Đặt được mức này vì tên file là hash của chính đoạn văn bản: sửa câu thì hash
    /// đổi và URL cũng đổi, nên một URL đã trả về nội dung gì thì mãi mãi trả về đúng nội dung đó.
    /// </summary>
    private const int CacheSeconds = 31_536_000;

    public static void MapMedia(this WebApplication app)
    {
        app.MapGet("/api/v1/media/tts", Tts).WithTags("Media");
    }

    private static IResult Tts(
        string? text,
        ClaimsPrincipal principal,
        HttpContext http,
        IConfiguration configuration)
    {
        // Cùng mức bảo vệ với nội dung bài học: audio là một phần của giáo trình.
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length > TtsCatalogue.MaxTextLength)
        {
            return Results.BadRequest(new { error = "text_invalid" });
        }

        var hash = TtsCatalogue.HashOf(text);

        if (hash.Length == 0)
        {
            return Results.BadRequest(new { error = "text_invalid" });
        }

        var audioRoot = configuration.GetValue<string>("Storage:AudioRoot") ?? "media";

        var path = Path.Combine(
            audioRoot,
            TtsManifestWriter.DirectoryName,
            hash + TtsCatalogue.FileExtension);

        if (!File.Exists(path))
        {
            return Results.NotFound(new { error = "audio_not_generated" });
        }

        // private chứ không public: nội dung nằm sau đăng nhập nên proxy dùng chung không được
        // giữ bản sao rồi phát lại cho người chưa đăng nhập.
        http.Response.Headers[HeaderNames.CacheControl] = $"private, max-age={CacheSeconds}, immutable";

        return Results.File(path, contentType: "audio/wav", enableRangeProcessing: true);
    }
}
