using CvService.DTOs;

namespace CvService.Services;

public class CvSectionServiceImpl : ICvSectionService
{
    public Task<List<CvSectionDto>> GetByVersionIdAsync(Guid versionId, string userId)
    {
        // TODO: Implement fetching sections by version ID with ownership check
        return Task.FromResult(new List<CvSectionDto>());
    }

    public Task<CvSectionDto?> GetByTypeAsync(Guid versionId, string sectionType, string userId)
    {
        // TODO: Implement fetching section by type with ownership check
        return Task.FromResult<CvSectionDto?>(null);
    }

    public Task<CvSectionDto> UpsertAsync(Guid versionId, string sectionType, UpdateSectionDto dto, string userId)
    {
        // TODO: Implement section create/update (upsert pattern)
        throw new NotImplementedException();
    }

    public Task<bool> DeleteAsync(Guid id, string userId)
    {
        // TODO: Implement section deletion with ownership check
        return Task.FromResult(false);
    }
}
