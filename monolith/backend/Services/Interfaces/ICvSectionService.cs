using CV_Generator.Models;

namespace CV_Generator.Services;

public interface ICvSectionService
{
    Task<List<CvSection>> GetByVersionIdAsync(Guid versionId);
    Task<CvSection> CreateAsync(CvSection section);
    Task<CvSection?> UpdateAsync(Guid id, CvSection section);
    Task<bool> DeleteAsync(Guid id);
}
