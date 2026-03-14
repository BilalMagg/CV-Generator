using backend.src.features.experience.interfaces;
using backend.src.features.experience.entity;
using backend.src.features.experience.dto;
using AutoMapper;

namespace backend.src.features.experience.service;

public class ExperienceService : IExperienceService
{
    private readonly IExperienceRepository _repository;
    private readonly IMapper _mapper;

    public ExperienceService(IExperienceRepository repository, IMapper mapper)
    {
        _repository = repository;
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
