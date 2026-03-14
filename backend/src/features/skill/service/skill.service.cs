using backend.src.features.skill.interfaces;
using backend.src.features.skill.entity;
using backend.src.features.skill.dto;
using AutoMapper;

namespace backend.src.features.skill.service;

public class SkillService : ISkillService
{
    private readonly ISkillRepository _repository;
    private readonly IMapper _mapper;

    public SkillService(ISkillRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<SkillResponseDto>> GetAll()
    {
        var entities = await _repository.GetAll();
        return _mapper.Map<List<SkillResponseDto>>(entities);
    }

    public async Task<SkillResponseDto> GetById(Guid id)
    {
        var entity = await _repository.GetById(id);
        return _mapper.Map<SkillResponseDto>(entity);
    }

    public async Task<SkillResponseDto> Create(CreateSkillDto dto)
    {
        var entity = _mapper.Map<Skill>(dto);
        var created = await _repository.Create(entity);
        return _mapper.Map<SkillResponseDto>(created);
    }

    public async Task<SkillResponseDto> Update(Guid id, UpdateSkillDto dto)
    {
        var entity = await _repository.GetById(id);
        _mapper.Map(dto, entity);
        var updated = await _repository.Update(entity);
        return _mapper.Map<SkillResponseDto>(updated);
    }

    public async Task Delete(Guid id)
    {
        await _repository.Delete(id);
    }
}
