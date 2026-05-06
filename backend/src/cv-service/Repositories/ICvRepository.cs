using CvService.Entities;

namespace CvService.Repositories;

public interface ICvRepository
{
    Task<List<Cv>> GetAllByUserIdAsync(string userId);
    Task<Cv?> GetByIdAsync(Guid id);
    Task<Cv> CreateAsync(Cv cv);
    Task UpdateAsync(Cv cv);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> IsOwnedByUserAsync(Guid id, string userId);
}
