namespace CV_Generator.Services;

public interface ISearchSyncService
{
    Task<int> SyncUserAsync(Guid userId, CancellationToken ct = default);
    Task<int> SyncUserEntitiesAsync(Guid userId, string[] sourceTypes, CancellationToken ct = default);
    Task<SearchSyncStatus> GetStatusAsync(Guid userId, CancellationToken ct = default);
}

public record SearchSyncStatus(bool Synced, int ChunkCount, Dictionary<string, int> SourceTypeCounts);
