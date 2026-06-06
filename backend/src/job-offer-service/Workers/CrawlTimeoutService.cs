using Microsoft.EntityFrameworkCore;
using JobOfferService.Entities;
using JobOfferService.Repositories;

namespace JobOfferService.Workers;

public class CrawlTimeoutService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CrawlTimeoutService> _logger;
    private static readonly TimeSpan TimeoutDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

    public CrawlTimeoutService(
        IServiceScopeFactory scopeFactory,
        ILogger<CrawlTimeoutService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "CrawlTimeoutService started — checking every {Interval}s for searches stuck > {Timeout}min",
            CheckInterval.TotalSeconds, TimeoutDuration.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForStuckSearchesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for stuck crawls");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckForStuckSearchesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobOfferDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ISearchCacheRepository>();

        var cutoff = DateTime.UtcNow.Add(-TimeoutDuration);

        var stuck = await db.SearchCaches
            .Where(s => s.Status == SearchStatus.Extracting && s.UpdatedAt < cutoff)
            .ToListAsync();

        foreach (var search in stuck)
        {
            _logger.LogWarning(
                "Search {SearchId} stuck in Extracting since {UpdatedAt} — marking Failed",
                search.SearchId, search.UpdatedAt);

            await repo.MarkAsFailedAsync(search.SearchId);
        }

        if (stuck.Count > 0)
        {
            _logger.LogInformation("Auto-failed {Count} stuck searches", stuck.Count);
        }
    }
}
