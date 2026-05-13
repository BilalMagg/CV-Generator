using CvService.DTOs;

namespace CvService.Services;

public interface ICvSectionService
{
    Task<List<CvSectionDto>> GetByVersionIdAsync(Guid versionId, string userId);
    Task<CvSectionDto?> GetByTypeAsync(Guid versionId, string sectionType, string userId);
    Task<CvSectionDto> UpsertAsync(Guid versionId, string sectionType, UpdateSectionDto dto, string userId);
    Task<bool> DeleteAsync(Guid id, string userId);
}
