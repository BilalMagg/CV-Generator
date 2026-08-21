using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

public interface ICvService
{
    Task<List<Cv>> GetAllAsync(Guid userId);
    Task<Cv?> GetByIdAsync(Guid id, Guid userId);
    Task<Cv> CreateAsync(CreateCvDto dto, Guid userId);
    Task<Cv?> UpdateAsync(Guid id, UpdateCvDto dto, Guid userId);
    Task<bool> DeleteAsync(Guid id, Guid userId);
}
