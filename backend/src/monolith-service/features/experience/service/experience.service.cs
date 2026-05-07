using backend.src.features.experience.interfaces;
using backend.src.features.experience.entity;
using backend.src.features.experience.dto;
using AutoMapper;
using backend.src.features.user.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.experience.service;

public class ExperienceService : IExperienceService
{
    private readonly IExperienceRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public ExperienceService(IExperienceRepository repository, IUserRepository userRepository, IMapper mapper)
    {
        _repository = repository;
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<List<ExperienceResponseDto>> GetAll()
    {
        var entities = await _repository.GetAll();
        return _mapper.Map<List<ExperienceResponseDto>>(entities);
    }

    public async Task<ExperienceResponseDto> GetById(Guid id)
    {
        var entity = await _repository.GetById(id);
        return _mapper.Map<ExperienceResponseDto>(entity);
    }

    public async Task<ExperienceResponseDto> Create(CreateExperienceDto dto)
    {
        var user = await _userRepository.GetById(dto.UserId);
        if (user == null)
            throw new NotFoundException($"User with ID {dto.UserId} not found");

        var entity = _mapper.Map<Experience>(dto);
        var created = await _repository.Create(entity);
        return _mapper.Map<ExperienceResponseDto>(created);
    }

    public async Task<ExperienceResponseDto> Update(Guid id, UpdateExperienceDto dto)
    {
        var entity = await _repository.GetById(id);
        _mapper.Map(dto, entity);
        var updated = await _repository.Update(entity);
        return _mapper.Map<ExperienceResponseDto>(updated);
    }

    public async Task Delete(Guid id)
    {
        await _repository.Delete(id);
    }
}
