using CvService.DTOs;

namespace CvService.Services;

public interface ICvVersionService
{
    Task<List<CvVersionDto>> GetByCvIdAsync(Guid cvId, string userId);
    Task<CvVersionDto?> GetByIdAsync(Guid id, string userId);
    Task<CvVersionDto> CreateAsync(Guid cvId, CreateCvVersionDto dto, string userId);
    Task<bool> DeleteAsync(Guid id, string userId);
}
