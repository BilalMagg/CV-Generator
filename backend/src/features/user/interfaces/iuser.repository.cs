public interface IUserRepository
{
    Task<List<UserEntity>> GetAll();
}
