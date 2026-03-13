public interface IProjectRepository
{
    Task<List<ProjectEntity>> GetAll();
}
