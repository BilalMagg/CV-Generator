using CV_Generator.Models;

namespace CV_Generator.Services;

public interface IUserQuotaRepository
{
    Task<UserQuota?> GetByUserIdAsync(Guid userId);
    Task<UserQuota> CreateAsync(UserQuota quota);
    Task UpdateAsync(UserQuota quota);
}
