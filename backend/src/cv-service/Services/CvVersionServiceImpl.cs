using CvService.DTOs;
using CvService.Entities;
using CvService.Repositories;

namespace CvService.Services;

public class CvVersionServiceImpl : ICvVersionService
{
    private readonly ICvVersionRepository _versionRepo;
    private readonly ICvRepository _cvRepo;
    private readonly ILogger<CvVersionServiceImpl> _logger;

    public CvVersionServiceImpl(
        ICvVersionRepository versionRepo,
        ICvRepository cvRepo,
        ILogger<CvVersionServiceImpl> logger)
    {
        _versionRepo = versionRepo;
        _cvRepo = cvRepo;
        _logger = logger;
    }

    public async Task<List<CvVersionDto>> GetByCvIdAsync(Guid cvId, string userId)
    {
        if (!await _cvRepo.IsOwnedByUserAsync(cvId, userId)) return new List<CvVersionDto>();

        var versions = await _versionRepo.GetByCvIdAsync(cvId);
        return versions.Select(MapToDto).ToList();
    }

    public async Task<CvVersionDto?> GetByIdAsync(Guid id, string userId)
    {
        var version = await _versionRepo.GetByIdAsync(id);
        if (version == null || !await _cvRepo.IsOwnedByUserAsync(version.CvId, userId)) return null;
        return MapToDto(version);
    }

    public async Task<CvVersionDto> CreateAsync(Guid cvId, CreateCvVersionDto dto, string userId)
    {
        if (!await _cvRepo.IsOwnedByUserAsync(cvId, userId))
            throw new UnauthorizedAccessException($"CV {cvId} not found or not owned by user");

        var version = new CvVersion
        {
            Id = Guid.NewGuid(),
            CvId = cvId,
            VersionNumber = await _versionRepo.GetNextVersionNumberAsync(cvId),
            Label = dto.Label,
            ContentJson = dto.ContentJson ?? "{}",
            CreatedAt = DateTime.UtcNow
        };

        await _versionRepo.CreateAsync(version);
        return MapToDto(version);
    }

    public async Task<bool> DeleteAsync(Guid id, string userId)
    {
        var version = await _versionRepo.GetByIdAsync(id);
        if (version == null || !await _cvRepo.IsOwnedByUserAsync(version.CvId, userId)) return false;
        return await _versionRepo.DeleteAsync(id);
    }

    private static CvVersionDto MapToDto(CvVersion v) => new(
        v.Id, v.CvId, v.VersionNumber, v.Label, v.FileUrl, v.PdfUrl, v.ContentJson, v.CreatedAt
    );
}
