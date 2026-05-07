using CvService.DTOs;

namespace CvService.Services;

public class CvVersionServiceImpl : ICvVersionService
{
    public Task<List<CvVersionDto>> GetByCvIdAsync(Guid cvId, string userId)
    {
        // TODO: Implement fetching versions by CV ID with ownership check
        return Task.FromResult(new List<CvVersionDto>());
    }

    public Task<CvVersionDto?> GetByIdAsync(Guid id, string userId)
    {
        // TODO: Implement fetching version by ID with ownership check
        return Task.FromResult<CvVersionDto?>(null);
    }

    public Task<CvVersionDto> CreateAsync(Guid cvId, CreateCvVersionDto dto, string userId)
    {
        // TODO: Implement version creation (copy from latest or create fresh)
        throw new NotImplementedException();
    }

    public Task<bool> DeleteAsync(Guid id, string userId)
    {
        // TODO: Implement version deletion with ownership check
        return Task.FromResult(false);
    }
}
