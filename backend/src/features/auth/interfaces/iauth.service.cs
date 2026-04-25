using System.Security.Claims;
using backend.src.features.auth.dto;
using backend.src.features.user.entity;

namespace backend.src.features.auth.interfaces;

public interface IAuthService
{
    Task<User> ProvisionUserAsync(ClaimsPrincipal principal);
    Task<User?> GetUserFromClaimsAsync(ClaimsPrincipal principal);
    Task<LoginResponseDto> BuildLoginResponseAsync(ClaimsPrincipal principal, string? returnUrl = null);
}
