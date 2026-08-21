using CV_Generator.Models;

namespace CV_Generator.Services;

public interface ICalendarConfigurationService
{
    Task<ApplicationConfiguration?> GetByUserIdAsync(Guid userId);
    Task<ApplicationConfiguration> CreateOrUpdateAsync(Guid userId, ApplicationConfiguration config);
}
