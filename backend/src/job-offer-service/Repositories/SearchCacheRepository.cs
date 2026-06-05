using Microsoft.EntityFrameworkCore;
using JobOfferService.Entities;
using JobOfferService.DTOs;

namespace JobOfferService.Repositories;

public interface ISearchCacheRepository
{
    Task<SearchCache?> GetBySearchIdAsync(Guid searchId);
    /// <summary>
    /// Returns a cache entry for the same keyword + location crawled today (UTC),
    /// if its status is not Failed (failed searches can be re-triggered).
    /// </summary>
    Task<SearchCache?> GetTodayCacheAsync(string keyword, string location);
    Task<SearchCache> CreateAsync(SearchCache cache);
    Task<SearchCache> UpdateAsync(SearchCache cache);
    Task<bool> IncrementProcessedCountAsync(Guid searchId);
    Task<List<SearchCache>> GetCrawlsByUserIdAsync(Guid userId);
    Task<CrawlPollResponseDto?> GetCrawlJobsAsync(Guid searchId);
    Task CreateSearchJobMatchAsync(Guid searchId, Guid jobOfferId);
    Task MarkAsFailedAsync(Guid searchId);
}

public class SearchCacheRepository : ISearchCacheRepository
{
    private readonly JobOfferDbContext _db;
    private readonly ILogger<SearchCacheRepository> _logger;

    public SearchCacheRepository(JobOfferDbContext db, ILogger<SearchCacheRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SearchCache?> GetBySearchIdAsync(Guid searchId)
        => await _db.SearchCaches.FindAsync(searchId);

    public async Task<SearchCache?> GetTodayCacheAsync(string keyword, string location)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var kw = keyword.Trim().ToLowerInvariant();
        var loc = location.Trim().ToLowerInvariant();

        return await _db.SearchCaches
            .Where(s => s.CrawledDate == today
                     && s.Keyword.ToLower() == kw
                     && s.Location != null && s.Location.ToLower() == loc
                     && s.Status != SearchStatus.Failed)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<SearchCache> CreateAsync(SearchCache cache)
    {
        _db.SearchCaches.Add(cache);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created SearchCache for SearchId {SearchId}", cache.SearchId);
        return cache;
    }

    public async Task<SearchCache> UpdateAsync(SearchCache cache)
    {
        cache.UpdatedAt = DateTime.UtcNow;
        _db.SearchCaches.Update(cache);
        await _db.SaveChangesAsync();
        return cache;
    }

    /// <summary>
    /// Atomically increments ProcessedCount and returns the updated entity.
    /// Uses ExecuteUpdateAsync (EF Core 7+) to avoid load→modify→save races.
    /// Returns true if the row was found and updated.
    /// </summary>
    public async Task<bool> IncrementProcessedCountAsync(Guid searchId)
    {
        var rows = await _db.SearchCaches
            .Where(s => s.SearchId == searchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ProcessedCount, x => x.ProcessedCount + 1)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));

        return rows > 0;
    }

    public async Task<List<SearchCache>> GetCrawlsByUserIdAsync(Guid userId)
    {
        return await _db.SearchCaches
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task CreateSearchJobMatchAsync(Guid searchId, Guid jobOfferId)
    {
        _db.SearchJobMatches.Add(new SearchJobMatch
        {
            Id = Guid.NewGuid(),
            SearchId = searchId,
            JobId = jobOfferId,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Linked search {SearchId} to job {JobId}", searchId, jobOfferId);
    }

    public async Task MarkAsFailedAsync(Guid searchId)
    {
        var cache = await _db.SearchCaches.FindAsync(searchId);
        if (cache is null)
        {
            _logger.LogWarning("MarkAsFailed: search {SearchId} not found", searchId);
            return;
        }
        cache.Status = SearchStatus.Failed;
        cache.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Search {SearchId} marked as Failed", searchId);
    }

    public async Task<CrawlPollResponseDto?> GetCrawlJobsAsync(Guid searchId)
    {
        var cache = await _db.SearchCaches.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SearchId == searchId);
        if (cache is null) return null;

        var jobs = await _db.Database.SqlQueryRaw<CrawlJobDto>(
            @"SELECT j.""Id"" AS JobId, j.""JobRole"" AS Title, j.""EnterpriseName"" AS Company,
                     j.""Location"", COALESCE(j.""SourceUrl"", '') AS JobUrl, 0.0 AS Confidence
              FROM ""search_job_matches"" m
              JOIN ""job_offers"" j ON m.""JobId"" = j.""Id""
              WHERE m.""SearchId"" = {0}",
            searchId
        ).ToListAsync();

        return new CrawlPollResponseDto(
            cache.SearchId, cache.Status.ToString(), cache.Keyword,
            cache.Location, cache.ExpectedCount, cache.ProcessedCount, jobs
        );
    }
}
