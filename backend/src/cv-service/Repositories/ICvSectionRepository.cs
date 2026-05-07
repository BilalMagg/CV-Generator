using CvService.Entities;

namespace CvService.Repositories;

public interface ICvSectionRepository
{
    Task<List<CvSection>> GetByVersionIdAsync(Guid versionId);
    Task<CvSection?> GetByTypeAsync(Guid versionId, string sectionType);
    Task<CvSection> CreateAsync(CvSection section);
    Task UpdateAsync(CvSection section);
    Task<bool> DeleteAsync(Guid id);
}
