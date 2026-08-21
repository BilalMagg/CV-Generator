using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

public interface IApplicationService
{
    Task<List<Application>> GetAllAsync(Guid userId);
    Task<Application?> GetByIdAsync(Guid id, Guid userId);
    Task<Application> CreateAsync(CreateApplicationDto dto, Guid userId);
    Task<Application?> UpdateAsync(Guid id, UpdateApplicationDto dto, Guid userId);
    Task<Application?> UpdateStatusAsync(Guid id, UpdateStatusDto dto, Guid userId);
    Task<bool> DeleteAsync(Guid id, Guid userId);
}
