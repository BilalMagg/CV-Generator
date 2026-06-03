using Microsoft.EntityFrameworkCore;
using CvService.Entities;

namespace CvService.Repositories;

public class CvVersionRepository : ICvVersionRepository
{
    private readonly CvDbContext _db;
    private readonly ILogger<CvVersionRepository> _logger;

    public CvVersionRepository(CvDbContext db, ILogger<CvVersionRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<CvVersion>> GetByCvIdAsync(Guid cvId)
        => await _db.CvVersions
            .Where(v => v.CvId == cvId)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();

    public async Task<CvVersion?> GetByIdAsync(Guid id)
        => await _db.CvVersions.FindAsync(id);

    public async Task<int> GetNextVersionNumberAsync(Guid cvId)
        => (await _db.CvVersions
            .Where(v => v.CvId == cvId)
            .MaxAsync(v => (int?)v.VersionNumber) ?? 0) + 1;

    public async Task<CvVersion> CreateAsync(CvVersion version)
    {
        _db.CvVersions.Add(version);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created CV version {Id} for CV {CvId}", version.Id, version.CvId);
        return version;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var version = await _db.CvVersions.FindAsync(id);
        if (version == null) return false;

        _db.CvVersions.Remove(version);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted CV version {Id}", id);
        return true;
    }
}
