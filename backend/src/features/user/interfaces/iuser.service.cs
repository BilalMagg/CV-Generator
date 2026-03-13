public interface IUserService
{
    Task<List<UserEntity>> GetAll();
}
