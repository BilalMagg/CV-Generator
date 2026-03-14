using backend.src.features.skill.interfaces;
using backend.src.features.skill.entity;
using backend.src.features.skill.dto;
using AutoMapper;
using backend.src.features.user.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.skill.service;

public class SkillService : ISkillService
{
    private readonly ISkillRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public SkillService(ISkillRepository repository, IUserRepository userRepository, IMapper mapper)
    {
        _repository = repository;
        _userRepository = userRepository;
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
        var user = await _userRepository.GetById(dto.UserId);
        if (user == null)
            throw new NotFoundException($"User with ID {dto.UserId} not found");

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
