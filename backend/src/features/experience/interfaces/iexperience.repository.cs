using backend.src.features.experience.entity;

namespace backend.src.features.experience.interfaces;

public interface IExperienceRepository
{
    Task<List<Experience>> GetAll();
    Task<Experience> GetById(Guid id);
    Task<Experience> Create(Experience entity);
    Task<Experience> Update(Experience entity);
    Task Delete(Guid id);
}
