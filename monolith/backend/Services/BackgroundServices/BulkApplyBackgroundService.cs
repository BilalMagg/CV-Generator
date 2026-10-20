using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CV_Generator.Services.BackgroundServices;

/// <summary>
/// Sequential executor for bulk-apply runs. One run at a time across the process, each run
/// processed in its own DI scope; runs can be cancelled from the controller.
/// </summary>
public class BulkApplyBackgroundService : BackgroundService
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    /// <summary>How often the watchdog looks for runs whose worker died without finalising.</summary>
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeRuns = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BulkApplyBackgroundService> _logger;

    public BulkApplyBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BulkApplyBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EnqueueRunAsync(Guid runId)
    {
        await _channel.Writer.WriteAsync(runId);
        _logger.LogInformation("Enqueued bulk apply run {RunId}", runId);
    }

    public void CancelRun(Guid runId)
    {
        if (_activeRuns.TryRemove(runId, out var cts))
        {
            _logger.LogInformation("Cancelling bulk apply run {RunId}", runId);
            cts.Cancel();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run the watchdog alongside the queue reader: it finalises any run left "running"/
        // "pending" without a live worker (a task that silently died, a crash mid-run, etc.).
        var watchdog = WatchdogLoopAsync(stoppingToken);

        try
        {
            await foreach (var runId in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                // Awaited on purpose: one run at a time, and any failure is contained by
                // ProcessRunAsync itself (it always finalises the run before returning).
                await ProcessRunAsync(runId, stoppingToken);
            }
        }
        finally
        {
            try { await watchdog; } catch { /* shutting down */ }
        }
    }

    private async Task WatchdogLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(WatchdogInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await SweepOrphansAsync();
        }
    }

    /// <summary>
    /// Finalises runs that are still "running"/"pending" in the DB but have no live worker in
    /// this process. This is the last-resort guarantee that the UI never polls a dead run.
    /// </summary>
    private async Task SweepOrphansAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BulkApplyService>();
            await service.SweepOrphanedRunsAsync(_activeRuns.Keys.ToHashSet());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk apply watchdog sweep failed");
        }
    }

    private async Task ProcessRunAsync(Guid runId, CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _activeRuns.TryAdd(runId, cts);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BulkApplyService>();
            await service.ExecuteRunAsync(runId, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bulk apply run {RunId} was cancelled", runId);
            await FinalizeFromWorkerAsync(runId, "cancelled", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk apply run {RunId} failed with unhandled error", runId);
            // Never leave the run wedged on "running" — finalise it here as a safety net in
            // case ExecuteRunAsync's own finalisation did not run (e.g. it threw outside its
            // try, or the row save failed).
            await FinalizeFromWorkerAsync(runId, "failed", $"Run aborted: {ex.Message}");
        }
        finally
        {
            if (_activeRuns.TryRemove(runId, out var existing))
                existing.Dispose();
        }
    }

    /// <summary>
    /// Best-effort terminal-finalisation from the worker. Uses a fresh scope/token so it works
    /// even when the run's own token was cancelled, and swallows any failure to never crash the
    /// worker loop.
    /// </summary>
    private async Task FinalizeFromWorkerAsync(Guid runId, string status, string? error)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BulkApplyService>();
            await service.FinalizeRunAsync(runId, status, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk apply run {RunId}: could not finalise from worker", runId);
        }
    }
}
