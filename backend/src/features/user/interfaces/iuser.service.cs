namespace backend.src.features.user.interfaces;

using backend.src.features.user.entity;

public interface IUserService
{
    Task<List<User>> GetAll();
}
