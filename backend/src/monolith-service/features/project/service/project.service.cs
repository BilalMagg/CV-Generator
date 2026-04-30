using backend.src.features.project.interfaces;
using backend.src.features.project.entity;
using backend.src.features.project.dto;
using AutoMapper;
using backend.src.features.user.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.project.service;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public ProjectService(IProjectRepository repository, IUserRepository userRepository, IMapper mapper)
    {
        _repository = repository;
        _userRepository = userRepository;
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
        var user = await _userRepository.GetById(dto.UserId);
        if (user == null)
            throw new NotFoundException($"User with ID {dto.UserId} not found");

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
