using backend.src.features.experience.dto;

namespace backend.src.features.experience.interfaces;

public interface IExperienceService
{
    Task<List<ExperienceResponseDto>> GetAll();
    Task<ExperienceResponseDto> GetById(Guid id);
    Task<ExperienceResponseDto> Create(CreateExperienceDto dto);
    Task<ExperienceResponseDto> Update(Guid id, UpdateExperienceDto dto);
    Task Delete(Guid id);
}
