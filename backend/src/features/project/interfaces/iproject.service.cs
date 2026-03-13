public interface IProjectService
{
    Task<List<ProjectEntity>> GetAll();
}
