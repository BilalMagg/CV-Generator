using backend.src.features.skill.dto;

namespace backend.src.features.skill.interfaces;

public interface ISkillService
{
    Task<List<SkillResponseDto>> GetAll();
    Task<SkillResponseDto> GetById(Guid id);
    Task<SkillResponseDto> Create(CreateSkillDto dto);
    Task<SkillResponseDto> Update(Guid id, UpdateSkillDto dto);
    Task Delete(Guid id);
}
