using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using backend.src.features.auth.dto;
using backend.src.features.auth.interfaces;
using backend.src.features.user.entity;

namespace backend.src.features.auth.services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly KeycloakAdminService _keycloakAdmin;

    public AuthService(
        IAuthRepository authRepository,
        IHttpContextAccessor httpContextAccessor,
        KeycloakAdminService keycloakAdmin)
    {
        _authRepository = authRepository;
        _httpContextAccessor = httpContextAccessor;
        _keycloakAdmin = keycloakAdmin;
    }

    public async Task<User> ProvisionUserAsync(ClaimsPrincipal principal)
    {
        var keycloakId = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(keycloakId))
            throw new InvalidOperationException("No sub claim found from Keycloak");

        var existingUser = await _authRepository.GetByKeycloakIdAsync(keycloakId);

        if (existingUser != null)
        {
            existingUser.LastLogin = DateTime.UtcNow;
            return await _authRepository.UpdateAsync(existingUser);
        }

        var email = principal.FindFirst("email")?.Value;
        var username = principal.FindFirst("preferred_username")?.Value;
        var firstName = principal.FindFirst("given_name")?.Value ?? "Unknown";
        var lastName = principal.FindFirst("family_name")?.Value ?? "User";

        var user = new User
        {
            KeycloakId = keycloakId,
            Email = email ?? $"{username}@keycloak.local",
            FirstName = firstName,
            LastName = lastName,
            Role = Role.USER,
            CreatedAt = DateTime.UtcNow,
            LastLogin = DateTime.UtcNow
        };

        return await _authRepository.CreateAsync(user);
    }

    public async Task<User?> GetUserFromClaimsAsync(ClaimsPrincipal principal)
    {
        var keycloakId = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(keycloakId))
            return null;

        return await _authRepository.GetByKeycloakIdAsync(keycloakId);
    }

    public async Task<LoginResponseDto> BuildLoginResponseAsync(ClaimsPrincipal principal, string? returnUrl = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            throw new InvalidOperationException("HttpContext is unavailable");

        var keycloakId = principal.FindFirst("sub")?.Value ?? "";
        var roles = principal.FindAll("realm_access.roles")
            .Select(c => c.Value)
            .ToList();

        var user = await GetUserFromClaimsAsync(principal);
        if (user == null)
            user = await ProvisionUserAsync(principal);

        // Get tokens from authentication result
        var authResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        string? accessToken = null;
        string? refreshToken = null;
        string? idToken = null;
        string? expiresAt = null;

        if (authResult.Properties != null)
        {
            authResult.Properties.Items.TryGetValue("access_token", out accessToken);
            authResult.Properties.Items.TryGetValue("refresh_token", out refreshToken);
            authResult.Properties.Items.TryGetValue("id_token", out idToken);
            authResult.Properties.Items.TryGetValue("expires_at", out expiresAt);
        }

        return new LoginResponseDto
        {
            UserId = user.Id,
            KeycloakId = keycloakId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            Tokens = new TokenInfoDto
            {
                AccessToken = accessToken != null && accessToken.Length > 50
                    ? accessToken[..50] + "..."
                    : accessToken,
                RefreshToken = refreshToken != null && refreshToken.Length > 50
                    ? refreshToken[..50] + "..."
                    : refreshToken,
                IdToken = idToken != null && idToken.Length > 50
                    ? idToken[..50] + "..."
                    : idToken,
                ExpiresAt = expiresAt,
                HasRefreshToken = !string.IsNullOrEmpty(refreshToken)
            }
        };
    }

    public async Task<KeycloakUserCreationResult> RegisterUserAsync(RegisterRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password) ||
            string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName))
        {
            return new KeycloakUserCreationResult
            {
                Success = false,
                Error = "All fields are required"
            };
        }

        // Check if user already exists locally
        var existingLocalUser = await _authRepository.GetByEmailAsync(dto.Email);
        if (existingLocalUser != null)
        {
            return new KeycloakUserCreationResult
            {
                Success = false,
                Error = "User with this email already exists"
            };
        }

        // Create user in Keycloak via Admin API
        var result = await _keycloakAdmin.CreateUserAsync(
            dto.Email,
            dto.FirstName,
            dto.LastName,
            dto.Password);

        if (!result.Success)
        {
            return result;
        }

        // Pre-provision user in local DB (so they're ready when they first login)
        // They won't have a KeycloakId until they actually login via OIDC
        var user = new User
        {
            KeycloakId = result.UserId ?? "", // Will be updated on first login
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Role = Role.USER,
            CreatedAt = DateTime.UtcNow,
            LastLogin = null,
            IsActive = true
        };

        try
        {
            await _authRepository.CreateAsync(user);
        }
        catch
        {
            // If local creation fails, Keycloak user still exists
            // User can still login and we'll sync on callback
        }

        return result;
    }
}
