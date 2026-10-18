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
        await foreach (var runId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            _ = ProcessRunAsync(runId, stoppingToken);
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk apply run {RunId} failed with unhandled error", runId);
        }
        finally
        {
            if (_activeRuns.TryRemove(runId, out var existing))
                existing.Dispose();
        }
    }
}
