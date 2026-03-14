using backend.src.features.user.entity;

namespace backend.src.features.user.interfaces;

public interface IUserRepository
{
    Task<List<User>> GetAll();

    Task<User?> GetById(Guid id);

    Task<User?> GetByEmail(string email);

    Task<User> Create(User user);

    Task<User?> Update(User user);

    Task<bool> Delete(Guid id);
}