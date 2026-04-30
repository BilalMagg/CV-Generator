using Microsoft.EntityFrameworkCore;
using ApplicationService.Entities;

namespace ApplicationService.Repositories;

public interface IApplicationStatusHistoryRepository
{
    Task<ApplicationStatusHistory> CreateAsync(ApplicationStatusHistory history);
    Task<List<ApplicationStatusHistory>> GetByApplicationIdAsync(Guid applicationId);
}

public class ApplicationStatusHistoryRepository : IApplicationStatusHistoryRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ApplicationStatusHistoryRepository> _logger;

    public ApplicationStatusHistoryRepository(ApplicationDbContext db, ILogger<ApplicationStatusHistoryRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ApplicationStatusHistory> CreateAsync(ApplicationStatusHistory history)
    {
        _db.ApplicationStatusHistory.Add(history);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created status history for application {AppId}", history.ApplicationId);
        return history;
    }

    public async Task<List<ApplicationStatusHistory>> GetByApplicationIdAsync(Guid applicationId)
        => await _db.ApplicationStatusHistory
            .Where(h => h.ApplicationId == applicationId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
}