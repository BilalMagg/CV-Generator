public interface IAuthRepository
{
    Task<List<AuthEntity>> GetAll();
}
