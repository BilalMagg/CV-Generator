public class AuthService : IAuthService
{
    private readonly IAuthRepository _repository;

    public AuthService(IAuthRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<AuthEntity>> GetAll()
    {
        return await _repository.GetAll();
    }
}
