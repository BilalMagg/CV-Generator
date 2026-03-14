using backend.src.features.user.dto;

namespace backend.src.features.user.interfaces;

public interface IUserService
{
    Task<List<UserResponseDto>> GetAll();

    Task<UserResponseDto?> GetById(Guid id);

    Task<UserResponseDto> Create(CreateUserDto dto);

    Task<UserResponseDto?> Update(Guid id, UpdateUserDto dto);

    Task<bool> Delete(Guid id);
}