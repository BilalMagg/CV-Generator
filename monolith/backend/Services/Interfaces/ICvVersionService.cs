using CV_Generator.Models;

namespace CV_Generator.Services;

public interface ICvVersionService
{
    Task<List<CvVersion>> GetByCvIdAsync(Guid cvId);
    Task<CvVersion?> GetByIdAsync(Guid id);
    Task<CvVersion> CreateAsync(CvVersion version);
    Task<CvVersion?> UpdateAsync(Guid id, CvVersion version);
    Task<bool> DeleteAsync(Guid id);
}
