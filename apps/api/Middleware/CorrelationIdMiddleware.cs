using Serilog.Context;

namespace EnglishForIT.Api.Middleware;

/// <summary>
/// Gắn một mã tương quan cho mỗi request và đẩy vào cả log lẫn header phản hồi.
/// Khi học viên báo lỗi, họ đọc mã này và ta tìm được đúng dòng log.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // Tôn trọng mã do reverse proxy đặt, để một request đi qua nhiều tầng vẫn cùng mã.
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64)
        {
            correlationId = context.TraceIdentifier;
        }

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
