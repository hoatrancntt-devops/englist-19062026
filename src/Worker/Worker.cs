using EnglishForIT.Infrastructure.Ops;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishForIT.Worker;

/// <summary>
/// Vòng chạy các job định kỳ.
///
/// Một vòng lặp đơn thay vì thư viện lập lịch: tất cả job ở đây đều đo được bằng "đã tới
/// lượt chưa" và đều an toàn khi chạy lại, nên thêm một phụ thuộc chỉ để có cú pháp cron
/// là đổi lấy phức tạp mà không được gì.
///
/// <b>Chỉ chạy MỘT bản sao worker.</b> Các job không khoá hàng khi đọc, nên hai bản sao
/// sẽ cùng xử lý một mẻ thư. Khoá chống gửi trùng cứu được phần lớn trường hợp nhưng không
/// phải tất cả. Muốn chạy nhiều bản sao thì phải thêm advisory lock của Postgres.
/// </summary>
public class Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger) : BackgroundService
{
    /// <summary>Nhịp vòng lặp. Đủ dày để thư không nằm chờ lâu, đủ thưa để không quay CPU.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    private DateTimeOffset _lastHourlyRun = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDailyRun = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker khởi động, nhịp {Tick}", Tick);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            try
            {
                await RunTickAsync(now, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Một job hỏng không được làm chết worker: lượt sau vẫn phải chạy,
                // nếu không thì hộp thư đi đứng im mà không ai biết.
                logger.LogError(ex, "Một lượt job thất bại, sẽ thử lại ở lượt sau");
            }

            try
            {
                await Task.Delay(Tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Worker dừng");
    }

    private async Task RunTickAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        // Hộp thư đi chạy mỗi lượt: thư chờ càng lâu càng vô nghĩa.
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        var sent = await outbox.ProcessAsync(now, ct);

        if (sent > 0)
        {
            logger.LogInformation("Đã gửi {Count} thư", sent);
        }

        if (now - _lastHourlyRun >= TimeSpan.FromHours(1))
        {
            _lastHourlyRun = now;

            var reminders = scope.ServiceProvider.GetRequiredService<LearnerReminderJob>();
            await reminders.RunAsync(now, ct);
            await reminders.GrantWeeklyFreezeAsync(now, ct);
        }

        if (now - _lastDailyRun >= TimeSpan.FromHours(24))
        {
            _lastDailyRun = now;

            var retention = scope.ServiceProvider.GetRequiredService<AudioRetentionJob>();
            await retention.RunAsync(now, ct);
        }
    }
}
