namespace CV_Generator.Services;

public static class SearchSyncHelper
{
    public static void TriggerSync(IServiceScopeFactory scopeFactory, Guid userId, ILogger logger, string source)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ISearchSyncService>();
                await syncService.SyncUserAsync(userId);
                logger.LogDebug("Search sync completed for user {UserId} after {Source}", userId, source);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Search sync failed for user {UserId} after {Source}", userId, source);
            }
        });
    }
}
