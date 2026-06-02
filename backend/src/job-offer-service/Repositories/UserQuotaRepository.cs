using Microsoft.EntityFrameworkCore;
using JobOfferService.Entities;

namespace JobOfferService.Repositories;

public interface IUserQuotaRepository
{
    /// <summary>Returns true if the user has already triggered a crawl today (UTC).</summary>
    Task<bool> HasCrawledTodayAsync(Guid userId);

    /// <summary>
    /// Records a crawl for today. Creates the row if it doesn't exist, updates it if it does.
    /// </summary>
    Task UpsertAsync(Guid userId);
}

public class UserQuotaRepository : IUserQuotaRepository
{
    private readonly JobOfferDbContext _db;
    private readonly ILogger<UserQuotaRepository> _logger;

    public UserQuotaRepository(JobOfferDbContext db, ILogger<UserQuotaRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> HasCrawledTodayAsync(Guid userId)
    {
        // var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // return await _db.UserQuotas
        //     .AnyAsync(q => q.UserId == userId && q.LastCrawlDate == today);
        
        // for now always return false because quota system is not implemented yet
        return false;
    }

    public async Task UpsertAsync(Guid userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await _db.UserQuotas.FindAsync(userId);

        if (existing is null)
        {
            _db.UserQuotas.Add(new UserQuota { UserId = userId, LastCrawlDate = today });
        }
        else
        {
            existing.LastCrawlDate = today;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("UserQuota updated | UserId={UserId} Date={Date}", userId, today);
    }
}
