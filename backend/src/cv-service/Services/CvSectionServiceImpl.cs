using CvService.DTOs;
using CvService.Entities;
using CvService.Repositories;

namespace CvService.Services;

public class CvSectionServiceImpl : ICvSectionService
{
    private readonly ICvSectionRepository _sectionRepo;
    private readonly ICvVersionRepository _versionRepo;
    private readonly ICvRepository _cvRepo;
    private readonly ILogger<CvSectionServiceImpl> _logger;

    public CvSectionServiceImpl(
        ICvSectionRepository sectionRepo,
        ICvVersionRepository versionRepo,
        ICvRepository cvRepo,
        ILogger<CvSectionServiceImpl> logger)
    {
        _sectionRepo = sectionRepo;
        _versionRepo = versionRepo;
        _cvRepo = cvRepo;
        _logger = logger;
    }

    public async Task<List<CvSectionDto>> GetByVersionIdAsync(Guid versionId, string userId)
    {
        if (!await IsVersionOwnedByUserAsync(versionId, userId)) return new List<CvSectionDto>();

        var sections = await _sectionRepo.GetByVersionIdAsync(versionId);
        return sections.Select(MapToDto).ToList();
    }

    public async Task<CvSectionDto?> GetByTypeAsync(Guid versionId, string sectionType, string userId)
    {
        if (!await IsVersionOwnedByUserAsync(versionId, userId)) return null;

        var section = await _sectionRepo.GetByTypeAsync(versionId, sectionType);
        return section == null ? null : MapToDto(section);
    }

    public async Task<CvSectionDto> UpsertAsync(Guid versionId, string sectionType, UpdateSectionDto dto, string userId)
    {
        if (!await IsVersionOwnedByUserAsync(versionId, userId))
            throw new UnauthorizedAccessException($"Version {versionId} not found or not owned by user");

        var section = await _sectionRepo.GetByTypeAsync(versionId, sectionType);
        if (section == null)
        {
            section = new CvSection
            {
                Id = Guid.NewGuid(),
                VersionId = versionId,
                SectionType = sectionType,
                DisplayOrder = dto.DisplayOrder,
                ContentJson = dto.ContentJson,
                UpdatedAt = DateTime.UtcNow
            };
            await _sectionRepo.CreateAsync(section);
        }
        else
        {
            section.DisplayOrder = dto.DisplayOrder;
            section.ContentJson = dto.ContentJson;
            await _sectionRepo.UpdateAsync(section);
        }

        return MapToDto(section);
    }

    public async Task<bool> DeleteAsync(Guid id, string userId)
    {
        var section = await _sectionRepo.GetByIdAsync(id);
        if (section == null || !await IsVersionOwnedByUserAsync(section.VersionId, userId)) return false;
        return await _sectionRepo.DeleteAsync(id);
    }

    // A section belongs to the user iff its version's parent CV is owned by the user.
    private async Task<bool> IsVersionOwnedByUserAsync(Guid versionId, string userId)
    {
        var version = await _versionRepo.GetByIdAsync(versionId);
        if (version == null) return false;
        return await _cvRepo.IsOwnedByUserAsync(version.CvId, userId);
    }

    private static CvSectionDto MapToDto(CvSection s) => new(
        s.Id, s.VersionId, s.SectionType, s.DisplayOrder, s.ContentJson, s.UpdatedAt
    );
}
