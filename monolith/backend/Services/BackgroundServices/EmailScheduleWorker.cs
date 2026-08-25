using CV_Generator.Services;

namespace CV_Generator.Services.BackgroundServices;

/// <summary>
/// Ticks every minute and fires due email schedules through
/// EmailScheduleService.FireScheduleAsync, which owns the actual
/// send pipeline (Gmail send, history rows, cron advancement).
/// </summary>
public class EmailScheduleWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailScheduleWorker> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EmailScheduleWorker started (tick every {Seconds}s)", TickInterval.TotalSeconds);

        using var timer = new PeriodicTimer(TickInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email schedule tick failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessDueSchedulesAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var scheduleSvc = scope.ServiceProvider.GetRequiredService<EmailScheduleService>();

        var due = scheduleSvc.GetDueSchedules();
        if (due.Count == 0) return;

        logger.LogInformation("Firing {Count} due email schedule(s)", due.Count);

        foreach (var schedule in due)
        {
            ct.ThrowIfCancellationRequested();
            await scheduleSvc.FireScheduleAsync(schedule);
        }
    }
}
