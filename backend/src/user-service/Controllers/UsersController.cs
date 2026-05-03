using System.ComponentModel.DataAnnotations;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService;
using UserService.Entity;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly ILogger<UsersController> _logger;

    public UsersController(UserDbContext db, ILogger<UsersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users.ToListAsync();
        return Ok(ApiResponse<List<UserResponseDto>>.Ok(users.Select(ToDto).ToList()));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(ApiResponse<UserResponseDto>.Error("User not found"));
        return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var existing = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (existing)
            return Conflict(ApiResponse<UserResponseDto>.Error("Email already exists"));

        var user = new User
        {
            KeycloakId = dto.KeycloakId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Role = Enum.Parse<Role>(dto.Role),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created user {Id}", user.Id);
        return Created($"/api/users/{user.Id}", ApiResponse<UserResponseDto>.Created(ToDto(user)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(ApiResponse<UserResponseDto>.Error("User not found"));

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.PhoneNumber = dto.PhoneNumber;
        user.AvatarUrl = dto.AvatarUrl;
        user.PreferencesJson = dto.PreferencesJson;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(ApiResponse<object>.Error("User not found"));

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DTOs
    public record CreateUserDto(
        string KeycloakId,
        string FirstName,
        string LastName,
        string Email,
        string Role
    );

    public record UpdateUserDto(
        string FirstName,
        string LastName,
        string? PhoneNumber,
        string? AvatarUrl,
        string? PreferencesJson
    );

    public record UserResponseDto(
        Guid Id,
        string KeycloakId,
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string? BirthDate,
        string Role,
        string? AvatarUrl,
        DateTime CreatedAt,
        DateTime? LastLogin,
        bool IsActive,
        string? AiProfileDataJson,
        string? PreferencesJson
    );

    private static UserResponseDto ToDto(User u) => new(
        u.Id, u.KeycloakId, u.FirstName, u.LastName, u.Email,
        u.PhoneNumber, u.BirthDate?.ToString("O"), u.Role.ToString(),
        u.AvatarUrl, u.CreatedAt, u.LastLogin, u.IsActive,
        u.AiProfileDataJson, u.PreferencesJson
    );
}