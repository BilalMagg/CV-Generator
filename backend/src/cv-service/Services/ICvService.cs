using CvService.DTOs;

namespace CvService.Services;

public interface ICvService
{
    Task<List<CvDto>> GetAllAsync(string userId);
    Task<CvDto?> GetByIdAsync(Guid id, string userId);
    Task<CvDto> CreateAsync(CreateCvDto dto, string userId);
    Task<CvDto?> UpdateAsync(Guid id, UpdateCvDto dto, string userId);
    Task<bool> DeleteAsync(Guid id, string userId);
}
