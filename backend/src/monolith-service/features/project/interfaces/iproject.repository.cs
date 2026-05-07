using backend.src.features.project.entity;

namespace backend.src.features.project.interfaces;

public interface IProjectRepository
{
    Task<List<Project>> GetAll();
    Task<Project> GetById(Guid id);
    Task<Project> Create(Project entity);
    Task<Project> Update(Project entity);
    Task Delete(Guid id);
}
