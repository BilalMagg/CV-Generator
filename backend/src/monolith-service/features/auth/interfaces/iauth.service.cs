using System.Security.Claims;
using backend.src.features.auth.dto;
using backend.src.features.user.entity;

namespace backend.src.features.auth.interfaces;

public interface IAuthService
{
    Task<User> ProvisionUserAsync(ClaimsPrincipal principal);
    Task<User?> GetUserFromClaimsAsync(ClaimsPrincipal principal);
    Task<LoginResponseDto> BuildLoginResponseAsync(ClaimsPrincipal principal, string? returnUrl = null);
    Task<KeycloakUserCreationResult> RegisterUserAsync(RegisterRequestDto dto);
}

public class RegisterRequestDto
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public class KeycloakUserCreationResult
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string? Error { get; set; }
}
