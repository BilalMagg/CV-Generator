using Microsoft.EntityFrameworkCore;
using CvService.Entities;

namespace CvService.Repositories;

public class CvSectionRepository : ICvSectionRepository
{
    private readonly CvDbContext _db;
    private readonly ILogger<CvSectionRepository> _logger;

    public CvSectionRepository(CvDbContext db, ILogger<CvSectionRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<CvSection>> GetByVersionIdAsync(Guid versionId)
        => await _db.CvSections
            .Where(s => s.VersionId == versionId)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

    public async Task<CvSection?> GetByIdAsync(Guid id)
        => await _db.CvSections.FindAsync(id);

    public async Task<CvSection?> GetByTypeAsync(Guid versionId, string sectionType)
        => await _db.CvSections
            .FirstOrDefaultAsync(s => s.VersionId == versionId && s.SectionType == sectionType);

    public async Task<CvSection> CreateAsync(CvSection section)
    {
        _db.CvSections.Add(section);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created CV section {Id} ({Type}) for version {VersionId}",
            section.Id, section.SectionType, section.VersionId);
        return section;
    }

    public async Task UpdateAsync(CvSection section)
    {
        section.UpdatedAt = DateTime.UtcNow;
        _db.CvSections.Update(section);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated CV section {Id}", section.Id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var section = await _db.CvSections.FindAsync(id);
        if (section == null) return false;

        _db.CvSections.Remove(section);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted CV section {Id}", id);
        return true;
    }
}
