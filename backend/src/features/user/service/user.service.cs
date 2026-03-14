using backend.src.features.user.interfaces;
using backend.src.features.user.entity;
using backend.src.features.user.dto;
using AutoMapper;
using backend.src.shared.exceptions;

namespace backend.src.features.user.service;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;

    public UserService(IUserRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<UserResponseDto>> GetAll()
    {
        var users = await _repository.GetAll();

        return _mapper.Map<List<UserResponseDto>>(users);
    }

    public async Task<UserResponseDto> GetById(Guid id)
    {
        var user = await _repository.GetById(id);

        return  _mapper.Map<UserResponseDto>(user);
    }

    public async Task<UserResponseDto> Create(CreateUserDto dto)
    {
        var existingUser = await _repository.GetByEmail(dto.Email);
        
        if (existingUser != null)
            throw new ConflictException("User with this email already exists.");

        var user = _mapper.Map<User>(dto);

        var created = await _repository.Create(user);

        return  _mapper.Map<UserResponseDto>(created);
    }

    public async Task<UserResponseDto> Update(Guid id, UpdateUserDto dto)
    {
        var user = await _repository.GetById(id);

        _mapper.Map(dto, user);

        var updated = await _repository.Update(user);

        return _mapper.Map<UserResponseDto>(updated);
    }

    public async Task Delete(Guid id)
    {
        await _repository.Delete(id);
    }
}