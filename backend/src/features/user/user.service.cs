public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<UserEntity>> GetAll()
    {
        return await _repository.GetAll();
    }
}
