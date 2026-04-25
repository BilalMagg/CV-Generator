using backend.src.features.user.entity;

namespace backend.src.features.auth.interfaces;

public interface IAuthRepository
{
    Task<User?> GetByKeycloakIdAsync(string keycloakId);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
}
