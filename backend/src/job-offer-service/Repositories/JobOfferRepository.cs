using Microsoft.EntityFrameworkCore;
using JobOfferService.Entities;


namespace JobOfferService.Repositories;

public interface IJobOfferRepository
{
    Task<JobOffer?> GetByIdAsync(Guid id);
    Task<JobOffer?> GetByIdWithDetailsAsync(Guid id);
    Task<List<JobOffer>> GetAllAsync(Guid? userId, int page, int pageSize);
    Task<int> GetTotalCountAsync(Guid? userId);
    Task<JobOffer> CreateAsync(JobOffer jobOffer);
    Task<JobOffer> UpdateAsync(JobOffer jobOffer);
    Task<JobOffer> UpdateWithDetailsAsync(JobOffer jobOffer);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<Dictionary<JobOfferStatus, int>> GetStatisticsAsync(Guid? userId);
}

public class JobOfferRepository : IJobOfferRepository
{
    private readonly JobOfferDbContext _db;
    private readonly ILogger<JobOfferRepository> _logger;

    public JobOfferRepository(JobOfferDbContext db, ILogger<JobOfferRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<JobOffer?> GetByIdAsync(Guid id)
        => await _db.JobOffers.FindAsync(id);

    // This replaces GetByIdWithHistoryAsync. It loads the 3 child tables.
    public async Task<JobOffer?> GetByIdWithDetailsAsync(Guid id)
        => await _db.JobOffers
            .Include(j => j.Skills)
            .Include(j => j.Responsibilities)
            .Include(j => j.Benefits)
            .FirstOrDefaultAsync(j => j.Id == id);

    public async Task<List<JobOffer>> GetAllAsync(Guid? userId, int page, int pageSize)
    {
        var query = _db.JobOffers.AsQueryable();

        // Filter by the user who ingested the job offer
        if (userId.HasValue)
            query = query.Where(j => j.UserId == userId.Value);

        return await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(Guid? userId)
    {
        var query = _db.JobOffers.AsQueryable();
        if (userId.HasValue)
            query = query.Where(j => j.UserId == userId.Value);
        return await query.CountAsync();
    }

    public async Task<JobOffer> CreateAsync(JobOffer jobOffer)
    {
        _db.JobOffers.Add(jobOffer);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created job offer {Id}", jobOffer.Id);
        return jobOffer;
    }

    public async Task<JobOffer> UpdateAsync(JobOffer jobOffer)
    {
        jobOffer.UpdatedAt = DateTime.UtcNow;
        _db.JobOffers.Update(jobOffer);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated job offer {Id}", jobOffer.Id);
        return jobOffer;
    }
    public async Task<JobOffer> UpdateWithDetailsAsync(JobOffer jobOffer)
    {
        // 1. Always update the modification timestamp
        jobOffer.UpdatedAt = DateTime.UtcNow;
        
        // 2. We do NOT call _db.JobOffers.Update(jobOffer) here!
        // Why? Because in the Service layer, we fetched this jobOffer using GetByIdWithDetailsAsync().
        // That means Entity Framework Core is already "Tracking" this object in memory. 
        // When you modified the lists (.Clear(), .Add()) in the Service layer, EF Core was watching.
        
        // 3. Just save the tracked changes to PostgreSQL
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Successfully updated job offer {Id} along with its skills, responsibilities, and benefits.", jobOffer.Id);
        
        return jobOffer;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var jobOffer = await _db.JobOffers.FindAsync(id);
        if (jobOffer == null) return false;

        _db.JobOffers.Remove(jobOffer);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted job offer {Id}", id);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
        => await _db.JobOffers.AnyAsync(j => j.Id == id);

    public async Task<Dictionary<JobOfferStatus, int>> GetStatisticsAsync(Guid? userId)
    {
        var query = _db.JobOffers.AsQueryable();
        if (userId.HasValue)
            query = query.Where(j => j.UserId == userId.Value);

        return await query
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }
}