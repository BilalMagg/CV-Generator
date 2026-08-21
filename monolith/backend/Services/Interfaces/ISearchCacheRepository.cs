using CV_Generator.Models;

namespace CV_Generator.Services;

public interface ISearchCacheRepository
{
    Task<SearchCache?> GetByIdAsync(Guid searchId);
    Task<SearchCache> CreateAsync(SearchCache cache);
    Task UpdateAsync(SearchCache cache);
    Task DeleteAsync(Guid searchId);
}
