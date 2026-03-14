using backend.src.features.project.dto;

namespace backend.src.features.project.interfaces;

public interface IProjectService
{
    Task<List<ProjectResponseDto>> GetAll();
    Task<ProjectResponseDto> GetById(Guid id);
    Task<ProjectResponseDto> Create(CreateProjectDto dto);
    Task<ProjectResponseDto> Update(Guid id, UpdateProjectDto dto);
    Task Delete(Guid id);
}
