using Microsoft.EntityFrameworkCore;
using ApplicationService.Entities;

namespace ApplicationService.Repositories;

public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(Guid id);
    Task<Application?> GetByIdWithHistoryAsync(Guid id);
    Task<List<Application>> GetAllAsync(Guid? candidateId, int page, int pageSize);
    Task<int> GetTotalCountAsync(Guid? candidateId);
    Task<Application> CreateAsync(Application application);
    Task<Application> UpdateAsync(Application application);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<Dictionary<ApplicationStatus, int>> GetStatisticsAsync(Guid? candidateId);
}

public class ApplicationRepository : IApplicationRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ApplicationRepository> _logger;

    public ApplicationRepository(ApplicationDbContext db, ILogger<ApplicationRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Application?> GetByIdAsync(Guid id)
        => await _db.Applications.FindAsync(id);

    public async Task<Application?> GetByIdWithHistoryAsync(Guid id)
        => await _db.Applications
            .Include(a => a.StatusHistory.OrderByDescending(h => h.ChangedAt))
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<List<Application>> GetAllAsync(Guid? candidateId, int page, int pageSize)
    {
        var query = _db.Applications.AsQueryable();

        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);

        return await query
            .OrderByDescending(a => a.AppliedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(Guid? candidateId)
    {
        var query = _db.Applications.AsQueryable();
        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);
        return await query.CountAsync();
    }

    public async Task<Application> CreateAsync(Application application)
    {
        _db.Applications.Add(application);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created application {Id}", application.Id);
        return application;
    }

    public async Task<Application> UpdateAsync(Application application)
    {
        application.UpdatedAt = DateTime.UtcNow;
        _db.Applications.Update(application);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated application {Id}", application.Id);
        return application;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var application = await _db.Applications.FindAsync(id);
        if (application == null) return false;

        _db.Applications.Remove(application);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted application {Id}", id);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
        => await _db.Applications.AnyAsync(a => a.Id == id);

    public async Task<Dictionary<ApplicationStatus, int>> GetStatisticsAsync(Guid? candidateId)
    {
        var query = _db.Applications.AsQueryable();
        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);

        return await query
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }
}