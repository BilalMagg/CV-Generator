namespace backend.src.features.auth.services;

using backend.src.features.auth.interfaces;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _repository;

    public AuthService(IAuthRepository repository)
    {
        _repository = repository;
    }

}
