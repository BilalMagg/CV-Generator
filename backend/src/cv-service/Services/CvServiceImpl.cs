using CvService.DTOs;

namespace CvService.Services;

public class CvServiceImpl : ICvService
{
    public Task<List<CvDto>> GetAllAsync(string userId)
    {
        // TODO: Implement fetching all CVs for user
        return Task.FromResult(new List<CvDto>());
    }

    public Task<CvDto?> GetByIdAsync(Guid id, string userId)
    {
        // TODO: Implement fetching CV by ID with ownership check
        return Task.FromResult<CvDto?>(null);
    }

    public Task<CvDto> CreateAsync(CreateCvDto dto, string userId)
    {
        // TODO: Implement CV creation
        throw new NotImplementedException();
    }

    public Task<CvDto?> UpdateAsync(Guid id, UpdateCvDto dto, string userId)
    {
        // TODO: Implement CV update with ownership check
        return Task.FromResult<CvDto?>(null);
    }

    public Task<bool> DeleteAsync(Guid id, string userId)
    {
        // TODO: Implement soft/hard delete with ownership check
        return Task.FromResult(false);
    }
}
