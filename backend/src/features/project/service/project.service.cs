using backend.src.features.project.interfaces;
using backend.src.features.project.entity;
using backend.src.features.project.dto;
using AutoMapper;

namespace backend.src.features.project.service;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;
    private readonly IMapper _mapper;

    public ProjectService(IProjectRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<ProjectResponseDto>> GetAll()
    {
        var entities = await _repository.GetAll();
        return _mapper.Map<List<ProjectResponseDto>>(entities);
    }

    public async Task<ProjectResponseDto> GetById(Guid id)
    {
        var entity = await _repository.GetById(id);
        return _mapper.Map<ProjectResponseDto>(entity);
    }

    public async Task<ProjectResponseDto> Create(CreateProjectDto dto)
    {
        var entity = _mapper.Map<Project>(dto);
        var created = await _repository.Create(entity);
        return _mapper.Map<ProjectResponseDto>(created);
    }

    public async Task<ProjectResponseDto> Update(Guid id, UpdateProjectDto dto)
    {
        var entity = await _repository.GetById(id);
        _mapper.Map(dto, entity);
        var updated = await _repository.Update(entity);
        return _mapper.Map<ProjectResponseDto>(updated);
    }

    public async Task Delete(Guid id)
    {
        await _repository.Delete(id);
    }
}
