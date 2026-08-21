using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CV_Generator.Services.BackgroundServices;

public class CvGenerationBackgroundService : BackgroundService
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeRuns = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CvGenerationBackgroundService> _logger;

    public CvGenerationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CvGenerationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<Guid> EnqueueRunAsync(Guid runId)
    {
        await _channel.Writer.WriteAsync(runId);
        _logger.LogInformation("Enqueued CV generation run {RunId}", runId);
        return runId;
    }

    public void CancelRun(Guid runId)
    {
        if (_activeRuns.TryRemove(runId, out var cts))
        {
            _logger.LogInformation("Cancelling CV generation run {RunId}", runId);
            cts.Cancel();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new List<Task>();

        await foreach (var runId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            tasks.Add(ProcessRunAsync(runId, stoppingToken));
            tasks = tasks.Where(t => !t.IsCompleted).ToList();
        }
    }

    private async Task ProcessRunAsync(Guid runId, CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _activeRuns.TryAdd(runId, cts.Token.CanBeCanceled ? cts : new CancellationTokenSource());

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var executionService = scope.ServiceProvider.GetRequiredService<WorkflowExecutionService>();
            await executionService.ExecutePipelineAsync(runId, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Run {RunId} was cancelled", runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed with unhandled error", runId);
        }
        finally
        {
            if (_activeRuns.TryRemove(runId, out var existingCts))
            {
                existingCts.Dispose();
            }
        }
    }
}
