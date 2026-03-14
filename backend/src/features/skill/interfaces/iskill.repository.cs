using backend.src.features.skill.entity;

namespace backend.src.features.skill.interfaces;

public interface ISkillRepository
{
    Task<List<Skill>> GetAll();
    Task<Skill> GetById(Guid id);
    Task<Skill> Create(Skill entity);
    Task<Skill> Update(Skill entity);
    Task Delete(Guid id);
}
