using backend.src.features.user.interfaces;
using backend.src.features.user.entity;
using backend.src.features.user.dto;

namespace backend.src.features.user.service;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<UserResponseDto>> GetAll()
    {
        var users = await _repository.GetAll();

        return users.Select(u => MapToResponse(u)).ToList();
    }

    public async Task<UserResponseDto?> GetById(Guid id)
    {
        var user = await _repository.GetById(id);

        if (user == null)
            return null;

        return MapToResponse(user);
    }

    public async Task<UserResponseDto> Create(CreateUserDto dto)
    {
        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            PhoneNumber = dto.PhoneNumber,
            BirthDate = dto.BirthDate
        };

        var created = await _repository.Create(user);

        return MapToResponse(created);
    }

    public async Task<UserResponseDto?> Update(Guid id, UpdateUserDto dto)
    {
        var user = await _repository.GetById(id);

        if (user == null)
            return null;

        if (dto.FirstName != null)
            user.FirstName = dto.FirstName;

        if (dto.LastName != null)
            user.LastName = dto.LastName;

        if (dto.PhoneNumber != null)
            user.PhoneNumber = dto.PhoneNumber;

        if (dto.BirthDate != null)
            user.BirthDate = dto.BirthDate;

        if (dto.AvatarUrl != null)
            user.AvatarUrl = dto.AvatarUrl;

        var updated = await _repository.Update(user);

        return MapToResponse(updated!);
    }

    public async Task<bool> Delete(Guid id)
    {
        return await _repository.Delete(id);
    }

    private UserResponseDto MapToResponse(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }
}