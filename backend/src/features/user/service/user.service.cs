namespace backend.src.features.user.services;

using backend.src.features.user.interfaces;
using backend.src.features.user.entity;


public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<User>> GetAll()
    {
        return await _repository.GetAll();
    }
}
