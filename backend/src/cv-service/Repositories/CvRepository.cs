using Microsoft.EntityFrameworkCore;
using CvService.Entities;

namespace CvService.Repositories;

public class CvRepository : ICvRepository
{
    private readonly CvDbContext _db;
    private readonly ILogger<CvRepository> _logger;

    public CvRepository(CvDbContext db, ILogger<CvRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Cv>> GetAllByUserIdAsync(string userId)
        => await _db.Cvs
            .Where(c => c.UserId == Guid.Parse(userId))
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

    public async Task<Cv?> GetByIdAsync(Guid id)
        => await _db.Cvs
            .Include(c => c.Versions)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Cv> CreateAsync(Cv cv)
    {
        _db.Cvs.Add(cv);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created CV {Id}", cv.Id);
        return cv;
    }

    public async Task UpdateAsync(Cv cv)
    {
        cv.UpdatedAt = DateTime.UtcNow;
        _db.Cvs.Update(cv);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated CV {Id}", cv.Id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var cv = await _db.Cvs.FindAsync(id);
        if (cv == null) return false;

        _db.Cvs.Remove(cv);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted CV {Id}", id);
        return true;
    }

    public async Task<bool> IsOwnedByUserAsync(Guid id, string userId)
        => await _db.Cvs.AnyAsync(c => c.Id == id && c.UserId == Guid.Parse(userId));
}
