namespace backend.src.features.user.interfaces;

using backend.src.features.user.entity;
public interface IUserRepository
{
    Task<List<User>> GetAll();
}
