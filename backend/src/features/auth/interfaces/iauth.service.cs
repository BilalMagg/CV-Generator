public interface IAuthService
{
    Task<List<AuthEntity>> GetAll();
}
