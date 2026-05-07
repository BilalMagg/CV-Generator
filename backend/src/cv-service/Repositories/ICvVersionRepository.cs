using CvService.Entities;

namespace CvService.Repositories;

public interface ICvVersionRepository
{
    Task<List<CvVersion>> GetByCvIdAsync(Guid cvId);
    Task<CvVersion?> GetByIdAsync(Guid id);
    Task<int> GetNextVersionNumberAsync(Guid cvId);
    Task<CvVersion> CreateAsync(CvVersion version);
    Task<bool> DeleteAsync(Guid id);
}
