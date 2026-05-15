·
7/app/src/monolith-service/config/KeycloakAdminConfig.csênamespace backend.src.config;

public static class KeycloakAdminConfig
{
    public static string AdminUrl { get; } =
        Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_URL") ?? "http://keycloak:8080";

    public static string AdminUsername { get; } =
        Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_USERNAME") ?? "admin";

    public static string AdminPassword { get; } =
        Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_PASSWORD") ?? "";

    public static string AdminClientId { get; } =
        Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_CLIENT_ID") ?? "admin-cli";

    public static string Realm => KeycloakConfig.Realm;

    public static string TokenUrl =>
        $"{AdminUrl.TrimEnd('/')}/realms/{Realm}/protocol/openid-connect/token";

    public static string UsersUrl =>
        $"{AdminUrl.TrimEnd('/')}/admin/realms/{Realm}/users";
}
ParseOptions.0.json≥

2/app/src/monolith-service/config/KeycloakConfig.csÁ	namespace backend.src.config
{
    public static class KeycloakConfig
    {
        public static string Authority { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_AUTHORITY") ?? "";
        public static string ClientId { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_ID") ?? "";
        public static string ClientSecret { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_SECRET") ?? "";
        public static string ResponseType { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_RESPONSE") ?? "code";
        public static string CallbackPath { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_CALLBACK_PATH") ?? "/signin-oidc";
        public static bool RequireHttpsMetadata { get; } =
            Environment.GetEnvironmentVariable("KEYCLOAK_REQUIRE_HTTPS")?.ToLower() == "true";

        public static string Realm => Authority.Contains("/realms/")
            ? Authority.Split("/realms/")[1].TrimEnd('/')
            : "";

        public static string MetadataAddress =>
            $"{Authority.TrimEnd('/')}/.well-known/openid-configuration";

        public static string MetadataAddressWithDocker =>
            Authority.Replace("keycloak", "host.docker.internal");
    }
}ParseOptions.0.jsonµ
6/app/src/monolith-service/features/auth/auth.module.csÂusing Microsoft.Extensions.DependencyInjection;
using backend.src.features.auth.interfaces;
using backend.src.features.auth.repository;
using backend.src.features.auth.services;

namespace backend.src.features.auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient<KeycloakAdminService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthRepository, AuthRepository>();

        return services;
    }
}
ParseOptions.0.json⁄8
E/app/src/monolith-service/features/auth/controller/auth.controller.cs˚7using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.src.config;
using backend.src.features.auth.dto;
using backend.src.features.auth.interfaces;
using backend.src.shared.responses;

namespace backend.src.features.auth.controller;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var redirectUri = Url.Action(nameof(Callback), "Auth", new { returnUrl }, Request.Scheme);
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded)
        {
            var error = result.Failure?.Message ?? "Authentication failed";
            return Unauthorized(ApiResponse<object>.ErrorResponse(error));
        }

        await _authService.ProvisionUserAsync(result.Principal!);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Ok(ApiResponse<object>.SuccessResponse(
            new { message = "Authenticated successfully" }));
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        // Get tokens for Keycloak logout
        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        string? idToken = null;
        if (authResult.Properties != null)
        {
            authResult.Properties.Items.TryGetValue("id_token", out idToken);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var authority = KeycloakConfig.Authority;
        var logoutUrl = $"{authority.TrimEnd('/')}/protocol/openid-connect/logout";

        var postLogoutRedirectUri = Url.Action(nameof(AfterLogout), "Auth", new { returnUrl }, Request.Scheme);

        var queryParams = new List<string>
        {
            $"post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri ?? "")}"
        };
        if (!string.IsNullOrEmpty(idToken))
            queryParams.Add($"id_token_hint={Uri.EscapeDataString(idToken)}");

        var keycloakLogoutUrl = $"{logoutUrl}?{string.Join("&", queryParams)}";

        return Redirect(keycloakLogoutUrl);
    }

    [HttpGet("after-logout")]
    public IActionResult AfterLogout([FromQuery] string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return Ok(ApiResponse<object>.SuccessResponse(
            new { message = "Logged out successfully" }));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var response = await _authService.BuildLoginResponseAsync(User);
        return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(response));
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> RefreshToken()
    {
        // Get tokens from authentication result
        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (authResult.Properties == null)
            return BadRequest(ApiResponse<object>.ErrorResponse("No authentication properties available"));

        authResult.Properties.Items.TryGetValue("refresh_token", out string? refreshToken);
        if (string.IsNullOrEmpty(refreshToken))
            return BadRequest(ApiResponse<object>.ErrorResponse("No refresh token available"));

        var tokenEndpoint = $"{KeycloakConfig.Authority.TrimEnd('/')}/protocol/openid-connect/token";
        var formData = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", KeycloakConfig.ClientId },
            { "client_secret", KeycloakConfig.ClientSecret },
            { "refresh_token", refreshToken }
        };

        using var client = new HttpClient();
        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(formData));

        if (!response.IsSuccessStatusCode)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized(ApiResponse<object>.ErrorResponse("Session expired, please login again"));
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenRefreshResponse>();
        if (tokenResponse == null)
            return StatusCode(500, ApiResponse<object>.ErrorResponse("Invalid token response"));

        var properties = authResult.Properties;

        properties.Items["access_token"] = tokenResponse.AccessToken;
        properties.Items["refresh_token"] = tokenResponse.RefreshToken ?? refreshToken;
        if (!string.IsNullOrEmpty(tokenResponse.IdToken))
            properties.Items["id_token"] = tokenResponse.IdToken;

        var expiresIn = int.Parse(tokenResponse.ExpiresIn);
        properties.Items["expires_at"] = DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToString();

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authResult.Principal!,
            properties);

        return Ok(ApiResponse<object>.SuccessResponse(
            new { expiresIn = tokenResponse.ExpiresIn }));
    }

    [HttpGet("denied")]
    public IActionResult Denied()
    {
        return StatusCode(403, ApiResponse<object>.ErrorResponse("Access denied"));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.ErrorResponse("Invalid request"));

        var result = await _authService.RegisterUserAsync(dto);

        if (!result.Success)
            return BadRequest(ApiResponse<object>.ErrorResponse(result.Error ?? "Registration failed"));

        return Ok(ApiResponse<object>.SuccessResponse(
            new { message = "User registered successfully. Please login." }));
    }

    private class TokenRefreshResponse
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string IdToken { get; set; } = "";
        public string ExpiresIn { get; set; } = "";
    }
}
ParseOptions.0.jsonÍ
7/app/src/monolith-service/features/auth/dto/auth.dto.csônamespace backend.src.features.auth.dto;

public class LoginResponseDto
{
    public Guid UserId { get; set; }
    public string KeycloakId { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
    public bool IsActive { get; set; }
    public TokenInfoDto Tokens { get; set; } = default!;
}

public class TokenInfoDto
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? IdToken { get; set; }
    public string? ExpiresAt { get; set; }
    public bool HasRefreshToken { get; set; }
}

public class UserClaimsDto
{
    public string? KeycloakId { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public List<string> Roles { get; set; } = new();
}
ParseOptions.0.json†
F/app/src/monolith-service/features/auth/interfaces/iauth.repository.cs¿using backend.src.features.user.entity;

namespace backend.src.features.auth.interfaces;

public interface IAuthRepository
{
    Task<User?> GetByKeycloakIdAsync(string keycloakId);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
}
ParseOptions.0.jsonÛ
C/app/src/monolith-service/features/auth/interfaces/iauth.service.csñusing System.Security.Claims;
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
ParseOptions.0.jsonÛ
E/app/src/monolith-service/features/auth/repository/auth.repository.csîusing Microsoft.EntityFrameworkCore;
using backend.src.features.auth.interfaces;
using backend.src.features.user.entity;

namespace backend.src.features.auth.repository;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;

    public AuthRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByKeycloakIdAsync(string keycloakId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }
}
ParseOptions.0.json”5
?/app/src/monolith-service/features/auth/service/auth.service.cs˙4using System.Security.Claims;
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
ParseOptions.0.jsonó
H/app/src/monolith-service/features/auth/services/KeycloakAdminService.csµusing System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.src.config;
using backend.src.features.auth.interfaces;

namespace backend.src.features.auth.services;

public class KeycloakAdminService
{
    private readonly HttpClient _httpClient;

    public KeycloakAdminService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> GetAdminTokenAsync()
    {
        var formData = new Dictionary<string, string>
        {
            { "grant_type", "password" },
            { "client_id", KeycloakAdminConfig.AdminClientId },
            { "username", KeycloakAdminConfig.AdminUsername },
            { "password", KeycloakAdminConfig.AdminPassword }
        };

        var response = await _httpClient.PostAsync(
            KeycloakAdminConfig.TokenUrl,
            new FormUrlEncodedContent(formData));

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return json?.AccessToken;
    }

    public async Task<KeycloakUserCreationResult> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string password)
    {
        var token = await GetAdminTokenAsync();
        if (string.IsNullOrEmpty(token))
            return new KeycloakUserCreationResult { Success = false, Error = "Failed to obtain admin token" };

        var userPayload = new
        {
            email,
            firstName,
            lastName,
            username = email,
            enabled = true,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = password,
                    temporary = false
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, KeycloakAdminConfig.UsersUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(userPayload),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var location = response.Headers.Location?.ToString();
            var userId = ExtractUserIdFromLocation(location);
            return new KeycloakUserCreationResult { Success = true, UserId = userId };
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        return new KeycloakUserCreationResult { Success = false, Error = errorContent };
    }

    private static string? ExtractUserIdFromLocation(string? location)
    {
        if (string.IsNullOrEmpty(location))
            return null;
        var parts = location.Split('/');
        return parts.Length > 0 ? parts[^1] : null;
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
    }
}
ParseOptions.0.json©
Q/app/src/monolith-service/features/experience/controller/experience.controller.csæusing Microsoft.AspNetCore.Mvc;
using backend.src.features.experience.interfaces;
using backend.src.features.experience.dto;
using backend.src.shared.responses;

namespace backend.src.features.experience.controller;

[ApiController]
[Route("api/experiences")]
public class ExperienceController : ControllerBase
{
    private readonly IExperienceService _service;

    public ExperienceController(IExperienceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAll();
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetById(id);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExperienceDto dto)
    {
        var result = await _service.Create(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Experience created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExperienceDto dto)
    {
        var result = await _service.Update(id, dto);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Experience deleted"));
    }
}
ParseOptions.0.jsonï
C/app/src/monolith-service/features/experience/dto/experience.dto.cs∏using System.ComponentModel.DataAnnotations;
using Pgvector;

namespace backend.src.features.experience.dto;

public class CreateExperienceDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = default!;

    [MaxLength(150)]
    public string? Company { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? ReferenceUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Ongoing";

    [Required]
    public Guid UserId { get; set; }
}

public class UpdateExperienceDto
{
    [MaxLength(150)]
    public string? Title { get; set; }

    [MaxLength(150)]
    public string? Company { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? ReferenceUrl { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }
}

public class ExperienceResponseDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = default!;

    public string? Company { get; set; }

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? ReferenceUrl { get; set; }

    public string Status { get; set; } = default!;

    public Guid UserId { get; set; }

    public string? AiSummaryJson { get; set; }

    /// <summary>384-dim pgvector embedding of the description. Used by the Python RAG agent.</summary>
    public Vector? DescriptionEmbedding { get; set; }
}
ParseOptions.0.jsonÊ
I/app/src/monolith-service/features/experience/entity/experience.entity.csÉusing System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.src.features.user.entity;
using Pgvector;

namespace backend.src.features.experience.entity
{
    [Table("experiences")]
    public class Experience
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(150)]
        public required string Title { get; set; } // Job title or role

        [MaxLength(150)]
        public string? Company { get; set; } // Company / Organization name

        [MaxLength(500)]
        public string? Description { get; set; } // Short description of tasks

        [Required]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; } // Nullable if ongoing

        // Optional link to reference or portfolio
        [MaxLength(300)]
        public string? ReferenceUrl { get; set; }

        // Status: ongoing / completed
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Ongoing";

        // Foreign key to the user
        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Optional: AI-generated summary or notes
        public string? AiSummaryJson { get; set; }

        // Vector embedding for RAG search (pgvector)
        public Vector? DescriptionEmbedding { get; set; }
    }
}ParseOptions.0.jsonå
B/app/src/monolith-service/features/experience/experience.mapper.cs∞using AutoMapper;
using backend.src.features.experience.entity;
using backend.src.features.experience.dto;

namespace backend.src.features.experience;

public class ExperienceMappingProfile : Profile
{
    public ExperienceMappingProfile()
    {
        CreateMap<Experience, ExperienceResponseDto>();
        CreateMap<CreateExperienceDto, Experience>();
        CreateMap<UpdateExperienceDto, Experience>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}
ParseOptions.0.jsonó
B/app/src/monolith-service/features/experience/experience.module.csªusing Microsoft.Extensions.DependencyInjection;
using backend.src.features.experience.interfaces;
using backend.src.features.experience.service;
using backend.src.features.experience.repository;

namespace backend.src.features.experience;

public static class ExperienceModule
{
    public static IServiceCollection AddExperienceModule(this IServiceCollection services)
    {
        services.AddScoped<IExperienceService, ExperienceService>();
        services.AddScoped<IExperienceRepository, ExperienceRepository>();

        return services;
    }
}
ParseOptions.0.jsonœ
R/app/src/monolith-service/features/experience/interfaces/iexperience.repository.cs„using backend.src.features.experience.entity;

namespace backend.src.features.experience.interfaces;

public interface IExperienceRepository
{
    Task<List<Experience>> GetAll();
    Task<Experience> GetById(Guid id);
    Task<Experience> Create(Experience entity);
    Task<Experience> Update(Experience entity);
    Task Delete(Guid id);
}
ParseOptions.0.jsoná
O/app/src/monolith-service/features/experience/interfaces/iexperience.service.csûusing backend.src.features.experience.dto;

namespace backend.src.features.experience.interfaces;

public interface IExperienceService
{
    Task<List<ExperienceResponseDto>> GetAll();
    Task<ExperienceResponseDto> GetById(Guid id);
    Task<ExperienceResponseDto> Create(CreateExperienceDto dto);
    Task<ExperienceResponseDto> Update(Guid id, UpdateExperienceDto dto);
    Task Delete(Guid id);
}
ParseOptions.0.jsonò
Q/app/src/monolith-service/features/experience/repository/experience.repository.cs≠using Microsoft.EntityFrameworkCore;
using backend.src.features.experience.entity;
using backend.src.features.experience.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.experience.repository;

public class ExperienceRepository : IExperienceRepository
{
    private readonly AppDbContext _context;

    public ExperienceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Experience>> GetAll()
    {
        return await _context.Set<Experience>().ToListAsync();
    }

    public async Task<Experience> GetById(Guid id)
    {
        var entity = await _context.Set<Experience>().FindAsync(id);
        
        if (entity == null)
            throw new NotFoundException($"Experience with ID {id} not found");
            
        return entity;
    }

    public async Task<Experience> Create(Experience entity)
    {
        _context.Set<Experience>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Experience> Update(Experience entity)
    {
        _context.Set<Experience>().Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(Guid id)
    {
        var entity = await GetById(id);

        _context.Set<Experience>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
ParseOptions.0.jsonö
K/app/src/monolith-service/features/experience/service/experience.service.csµusing backend.src.features.experience.interfaces;
using backend.src.features.experience.entity;
using backend.src.features.experience.dto;
using AutoMapper;
using backend.src.features.user.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.experience.service;

public class ExperienceService : IExperienceService
{
    private readonly IExperienceRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public ExperienceService(IExperienceRepository repository, IUserRepository userRepository, IMapper mapper)
    {
        _repository = repository;
        _userRepository = userRepository;
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
        var user = await _userRepository.GetById(dto.UserId);
        if (user == null)
            throw new NotFoundException($"User with ID {dto.UserId} not found");

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
ParseOptions.0.jsonˇ
K/app/src/monolith-service/features/project/controller/project.controller.csöusing Microsoft.AspNetCore.Mvc;
using backend.src.features.project.interfaces;
using backend.src.features.project.dto;
using backend.src.shared.responses;

namespace backend.src.features.project.controller;

[ApiController]
[Route("api/projects")]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _service;

    public ProjectController(IProjectService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAll();
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetById(id);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
    {
        var result = await _service.Create(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Project created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDto dto)
    {
        var result = await _service.Update(id, dto);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Project deleted"));
    }
}
ParseOptions.0.json˝
=/app/src/monolith-service/features/project/dto/project.dto.cs¶using System.ComponentModel.DataAnnotations;
using Pgvector;

namespace backend.src.features.project.dto;

public class CreateProjectDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = default!;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Role { get; set; }

    [MaxLength(1000)]
    public string? Achievements { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? RepositoryUrl { get; set; }

    [MaxLength(300)]
    public string? DemoUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Ongoing";

    [Required]
    public Guid UserId { get; set; }

    public string? SkillsJson { get; set; }
}

public class UpdateProjectDto
{
    [MaxLength(150)]
    public string? Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Role { get; set; }

    [MaxLength(1000)]
    public string? Achievements { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? RepositoryUrl { get; set; }

    [MaxLength(300)]
    public string? DemoUrl { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }

    public string? SkillsJson { get; set; }
}

public class ProjectResponseDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = default!;

    public string? Description { get; set; }

    public string? Role { get; set; }

    public string? Achievements { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? RepositoryUrl { get; set; }

    public string? DemoUrl { get; set; }

    public string Status { get; set; } = default!;

    public Guid UserId { get; set; }

    public string? SkillsJson { get; set; }

    public string? AiSummaryJson { get; set; }

    /// <summary>384-dim pgvector embedding of the description. Used by the Python RAG agent.</summary>
    public Vector? DescriptionEmbedding { get; set; }
}
ParseOptions.0.jsonÇ
C/app/src/monolith-service/features/project/entity/project.entity.cs•using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.src.features.user.entity;
using Pgvector;

namespace backend.src.features.project.entity
{
    [Table("projects")]
    public class Project
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid(); // Unique project ID

        [Required]
        [MaxLength(150)]
        public required string Title { get; set; } // Project name / title

        [MaxLength(1000)]
        public string? Description { get; set; } // Short description

        [MaxLength(500)]
        public string? Role { get; set; } // Role in the project (e.g., frontend dev, AI dev)

        [MaxLength(1000)]
        public string? Achievements { get; set; } // Optional key achievements

        [Required]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; } // Nullable, in case project is ongoing

        // Optional link to external resources
        [MaxLength(300)]
        public string? RepositoryUrl { get; set; }

        [MaxLength(300)]
        public string? DemoUrl { get; set; }

        // Project status (ongoing / completed / paused)
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Ongoing";

        // Foreign Key: user who owns/created this project
        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Optional: project tags or skills (JSON for scalability)
        public string? SkillsJson { get; set; }

        // Optional: AI-generated summary or notes
        public string? AiSummaryJson { get; set; }

        // Vector embedding for RAG search (pgvector)
        public Vector? DescriptionEmbedding { get; set; }
    }
}ParseOptions.0.jsonÆ
L/app/src/monolith-service/features/project/interfaces/iproject.repository.cs»using backend.src.features.project.entity;

namespace backend.src.features.project.interfaces;

public interface IProjectRepository
{
    Task<List<Project>> GetAll();
    Task<Project> GetById(Guid id);
    Task<Project> Create(Project entity);
    Task<Project> Update(Project entity);
    Task Delete(Guid id);
}
ParseOptions.0.jsonÊ
I/app/src/monolith-service/features/project/interfaces/iproject.service.csÉusing backend.src.features.project.dto;

namespace backend.src.features.project.interfaces;

public interface IProjectService
{
    Task<List<ProjectResponseDto>> GetAll();
    Task<ProjectResponseDto> GetById(Guid id);
    Task<ProjectResponseDto> Create(CreateProjectDto dto);
    Task<ProjectResponseDto> Update(Guid id, UpdateProjectDto dto);
    Task Delete(Guid id);
}
ParseOptions.0.jsonÂ
</app/src/monolith-service/features/project/project.mapper.csèusing AutoMapper;
using backend.src.features.project.entity;
using backend.src.features.project.dto;

namespace backend.src.features.project;

public class ProjectMappingProfile : Profile
{
    public ProjectMappingProfile()
    {
        CreateMap<Project, ProjectResponseDto>();
        CreateMap<CreateProjectDto, Project>();
        CreateMap<UpdateProjectDto, Project>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}
ParseOptions.0.jsonÛ
</app/src/monolith-service/features/project/project.module.csùusing Microsoft.Extensions.DependencyInjection;
using backend.src.features.project.interfaces;
using backend.src.features.project.service;
using backend.src.features.project.repository;

namespace backend.src.features.project;

public static class ProjectModule
{
    public static IServiceCollection AddProjectModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectRepository, ProjectRepository>();

        return services;
    }
}
ParseOptions.0.json‹
K/app/src/monolith-service/features/project/repository/project.repository.cs˜
using Microsoft.EntityFrameworkCore;
using backend.src.features.project.entity;
using backend.src.features.project.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.project.repository;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Project>> GetAll()
    {
        return await _context.Set<Project>().ToListAsync();
    }

    public async Task<Project> GetById(Guid id)
    {
        var entity = await _context.Set<Project>().FindAsync(id);
        
        if (entity == null)
            throw new NotFoundException($"Project with ID {id} not found");
            
        return entity;
    }

    public async Task<Project> Create(Project entity)
    {
        _context.Set<Project>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Project> Update(Project entity)
    {
        _context.Set<Project>().Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(Guid id)
    {
        var entity = await GetById(id);

        _context.Set<Project>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
ParseOptions.0.jsonÿ
E/app/src/monolith-service/features/project/service/project.service.cs˘using backend.src.features.project.interfaces;
using backend.src.features.project.entity;
using backend.src.features.project.dto;
using AutoMapper;
using backend.src.features.user.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.project.service;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public ProjectService(IProjectRepository repository, IUserRepository userRepository, IMapper mapper)
    {
        _repository = repository;
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<List<ProjectResponseDto>> GetAll()
    {
        var entities = await _repository.GetAll();
        return _mapper.Map<List<ProjectResponseDto>>(entities);
    }

    public async Task<ProjectResponseDto> GetById(Guid id)
    {
        var entity = await _repository.GetById(id);
        return _mapper.Map<ProjectResponseDto>(entity);
    }

    public async Task<ProjectResponseDto> Create(CreateProjectDto dto)
    {
        var user = await _userRepository.GetById(dto.UserId);
        if (user == null)
            throw new NotFoundException($"User with ID {dto.UserId} not found");

        var entity = _mapper.Map<Project>(dto);
        var created = await _repository.Create(entity);
        return _mapper.Map<ProjectResponseDto>(created);
    }

    public async Task<ProjectResponseDto> Update(Guid id, UpdateProjectDto dto)
    {
        var entity = await _repository.GetById(id);
        _mapper.Map(dto, entity);
        var updated = await _repository.Update(entity);
        return _mapper.Map<ProjectResponseDto>(updated);
    }

    public async Task Delete(Guid id)
    {
        await _repository.Delete(id);
    }
}
ParseOptions.0.json„
G/app/src/monolith-service/features/skill/controller/skill.controller.csÇusing Microsoft.AspNetCore.Mvc;
using backend.src.features.skill.interfaces;
using backend.src.features.skill.dto;
using backend.src.shared.responses;

namespace backend.src.features.skill.controller;

[ApiController]
[Route("api/skills")]
public class SkillController : ControllerBase
{
    private readonly ISkillService _service;

    public SkillController(ISkillService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAll();
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetById(id);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSkillDto dto)
    {
        var result = await _service.Create(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Skill created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSkillDto dto)
    {
        var result = await _service.Update(id, dto);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Skill deleted"));
    }
}
ParseOptions.0.jsoná

9/app/src/monolith-service/features/skill/dto/skill.dto.cs¥	using System.ComponentModel.DataAnnotations;
using Pgvector;

namespace backend.src.features.skill.dto;

public class CreateSkillDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;

    [MaxLength(50)]
    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }
}

public class UpdateSkillDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(50)]
    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }
}

public class SkillResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }

    public Guid UserId { get; set; }

    public string? Category { get; set; }

    /// <summary>384-dim pgvector embedding of the skill name. Used by the Python RAG agent.</summary>
    public Vector? NameEmbedding { get; set; }
}
ParseOptions.0.json’	
?/app/src/monolith-service/features/skill/entity/skill.entity.cs¸using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.src.features.user.entity;
using Pgvector;

namespace backend.src.features.skill.entity
{
    [Table("skills")]
    public class Skill
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public required string Name { get; set; } // e.g., "C#", "Angular", "Communication"

        [MaxLength(50)]
        public string? Level { get; set; } // Beginner / Intermediate / Advanced / Expert

        // Optional years of experience
        public int? YearsOfExperience { get; set; }

        // Optional link to user who owns this skill
        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Optional: category (technical, soft, language...)
        [MaxLength(50)]
        public string? Category { get; set; }

        // Vector embedding for RAG search (pgvector)
        public Vector? NameEmbedding { get; set; }
    }
}ParseOptions.0.jsonò
H/app/src/monolith-service/features/skill/interfaces/iskill.repository.cs∂using backend.src.features.skill.entity;

namespace backend.src.features.skill.interfaces;

public interface ISkillRepository
{
    Task<List<Skill>> GetAll();
    Task<Skill> GetById(Guid id);
    Task<Skill> Create(Skill entity);
    Task<Skill> Update(Skill entity);
    Task Delete(Guid id);
}
ParseOptions.0.json–
E/app/src/monolith-service/features/skill/interfaces/iskill.service.csÒusing backend.src.features.skill.dto;

namespace backend.src.features.skill.interfaces;

public interface ISkillService
{
    Task<List<SkillResponseDto>> GetAll();
    Task<SkillResponseDto> GetById(Guid id);
    Task<SkillResponseDto> Create(CreateSkillDto dto);
    Task<SkillResponseDto> Update(Guid id, UpdateSkillDto dto);
    Task Delete(Guid id);
}
ParseOptions.0.json¥
G/app/src/monolith-service/features/skill/repository/skill.repository.cs”
using Microsoft.EntityFrameworkCore;
using backend.src.features.skill.entity;
using backend.src.features.skill.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.skill.repository;

public class SkillRepository : ISkillRepository
{
    private readonly AppDbContext _context;

    public SkillRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Skill>> GetAll()
    {
        return await _context.Set<Skill>().ToListAsync();
    }

    public async Task<Skill> GetById(Guid id)
    {
        var entity = await _context.Set<Skill>().FindAsync(id);
        
        if (entity == null)
            throw new NotFoundException($"Skill with ID {id} not found");
            
        return entity;
    }

    public async Task<Skill> Create(Skill entity)
    {
        _context.Set<Skill>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Skill> Update(Skill entity)
    {
        _context.Set<Skill>().Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(Guid id)
    {
        var entity = await GetById(id);

        _context.Set<Skill>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
ParseOptions.0.json¨
A/app/src/monolith-service/features/skill/service/skill.service.cs—using backend.src.features.skill.interfaces;
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
ParseOptions.0.jsonÀ
8/app/src/monolith-service/features/skill/skill.mapper.cs˘using AutoMapper;
using backend.src.features.skill.entity;
using backend.src.features.skill.dto;

namespace backend.src.features.skill;

public class SkillMappingProfile : Profile
{
    public SkillMappingProfile()
    {
        CreateMap<Skill, SkillResponseDto>();
        CreateMap<CreateSkillDto, Skill>();
        CreateMap<UpdateSkillDto, Skill>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}
ParseOptions.0.json€
8/app/src/monolith-service/features/skill/skill.module.csâusing Microsoft.Extensions.DependencyInjection;
using backend.src.features.skill.interfaces;
using backend.src.features.skill.service;
using backend.src.features.skill.repository;

namespace backend.src.features.skill;

public static class SkillModule
{
    public static IServiceCollection AddSkillModule(this IServiceCollection services)
    {
        services.AddScoped<ISkillService, SkillService>();
        services.AddScoped<ISkillRepository, SkillRepository>();

        return services;
    }
}
ParseOptions.0.jsonœ
E/app/src/monolith-service/features/user/controller/user.controller.csusing Microsoft.AspNetCore.Mvc;
using backend.src.features.user.interfaces;
using backend.src.features.user.dto;
using backend.src.shared.responses;

namespace backend.src.features.user.controller;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IUserService _service;

    public UserController(IUserService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _service.GetAll();

        return Ok(ApiResponse<object>.SuccessResponse(users));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _service.GetById(id);

        return Ok(ApiResponse<object>.SuccessResponse(user));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var user = await _service.Create(dto);

        return Ok(ApiResponse<object>.SuccessResponse(user, "User created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var user = await _service.Update(id, dto);

        return Ok(ApiResponse<object>.SuccessResponse(user));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);

        return Ok(ApiResponse<object>.SuccessResponse(null, "User deleted"));
    }
}ParseOptions.0.jsonœ

7/app/src/monolith-service/features/user/dto/user.dto.cs˛	using System.ComponentModel.DataAnnotations;

namespace backend.src.features.user.dto;

public class CreateUserDto
{
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = default!;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;

    public string? PhoneNumber { get; set; }

    public DateTime? BirthDate { get; set; }
}

public class UpdateUserDto
{
    [MaxLength(50)]
    public string? FirstName { get; set; }

    [MaxLength(50)]
    public string? LastName { get; set; }

    public string? PhoneNumber { get; set; }

    public DateTime? BirthDate { get; set; }

    public string? AvatarUrl { get; set; }
}

public class UserResponseDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = default!;

    public string LastName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string Role { get; set; } = default!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
ParseOptions.0.jsonÁ
=/app/src/monolith-service/features/user/entity/user.entity.csêusing System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.src.features.skill.entity;
using backend.src.features.project.entity;
using backend.src.features.experience.entity;
using Microsoft.EntityFrameworkCore;

namespace backend.src.features.user.entity
{
    [Table("users")]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid(); // Id unique global
        
        [Required]
        [MaxLength(50)]
        public string KeycloakId { get; set; } = default!; // Link to Keycloak user

        [Required]
        [MaxLength(50)]
        public required string FirstName { get; set; }

        [Required]
        [MaxLength(50)]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public required string Email { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public DateTime? BirthDate { get; set; }

        // R√¥le pour g√©rer permissions (Admin / User / Recruiter ...)
        [Required]
        [MaxLength(20)]
        public Role Role { get; set; } = Role.USER;

        // Avatar / profile picture
        [MaxLength(255)]
        public string? AvatarUrl { get; set; }

        // Date d'inscription / cr√©ation du compte
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLogin { get; set; }

        // Statut actif ou d√©sactiv√©
        [Required]
        public bool IsActive { get; set; } = true;

        // Informations pour RAG / AI (optionnel, JSON ou table s√©par√©e)
        public string? AiProfileDataJson { get; set; }

        // Preferences de l'utilisateur (JSON pour scalabilit√©)
        public string? PreferencesJson { get; set; }

        // Relation avec CVs (1 user peut avoir plusieurs CV)
        // public virtual ICollection<CV> CVs { get; set; }

        // Relation avec Applications (1 user peut avoir plusieurs candidatures)
        // public virtual ICollection<Application> Applications { get; set; }

        // Tokens de s√©curit√© (optionnel)
        public virtual ICollection<Project>? Projects { get; set; }
        public virtual ICollection<Skill>? Skills { get; set; }
        public virtual ICollection<Experience>? Experiences { get; set; }
    }
    public enum Role
    {
        USER,
        ADMIN,
    }
}ParseOptions.0.jsonº
F/app/src/monolith-service/features/user/interfaces/iuser.repository.cs‹using backend.src.features.user.entity;

namespace backend.src.features.user.interfaces;

public interface IUserRepository
{
    Task<List<User>> GetAll();

    Task<User> GetById(Guid id);

    Task<User?> GetByEmail(string email);

    Task<User> Create(User user);

    Task<User> Update(User user);

    Task Delete(Guid id);
}ParseOptions.0.jsonÀ
C/app/src/monolith-service/features/user/interfaces/iuser.service.csÓusing backend.src.features.user.dto;

namespace backend.src.features.user.interfaces;

public interface IUserService
{
    Task<List<UserResponseDto>> GetAll();

    Task<UserResponseDto> GetById(Guid id);

    Task<UserResponseDto> Create(CreateUserDto dto);

    Task<UserResponseDto> Update(Guid id, UpdateUserDto dto);

    Task Delete(Guid id);
}ParseOptions.0.jsonÇ
E/app/src/monolith-service/features/user/repository/user.repository.cs£using Microsoft.EntityFrameworkCore;
using backend.src.features.user.entity;
using backend.src.features.user.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.user.repository;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<User>> GetAll()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<User> GetById(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        
        if (user == null)
            throw new NotFoundException($"User with ID {id} not found");
            
        return user;
    }

    public async Task<User?> GetByEmail(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> Create(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> Update(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task Delete(Guid id)
    {
        var user = await GetById(id);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }
}ParseOptions.0.json˘
?/app/src/monolith-service/features/user/service/user.service.cs†using backend.src.features.user.interfaces;
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
}ParseOptions.0.jsonì
6/app/src/monolith-service/features/user/user.mapper.cs√using AutoMapper;
using backend.src.features.user.entity;
using backend.src.features.user.dto;

namespace backend.src.features.user;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserResponseDto>();

        CreateMap<UpdateUserDto, User>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}ParseOptions.0.jsonÕ
6/app/src/monolith-service/features/user/user.module.cs˝using Microsoft.Extensions.DependencyInjection;
using backend.src.features.user.interfaces;
using backend.src.features.user.service;
using backend.src.features.user.repository;

namespace backend.src.features.user;
public static class UserModule
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
ParseOptions.0.jsonç
M/app/src/monolith-service/features/workflow/controller/workflow.controller.cs¶using Microsoft.AspNetCore.Mvc;
using backend.src.features.workflow.interfaces;
using backend.src.features.workflow.dto;
using backend.src.shared.responses;

namespace backend.src.features.workflow.controller;

[ApiController]
[Route("api/workflows")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _service;

    public WorkflowController(IWorkflowService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAll();
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetById(id);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDto dto)
    {
        var result = await _service.Create(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Workflow created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowDto dto)
    {
        var result = await _service.Update(id, dto);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Workflow deleted"));
    }
}
ParseOptions.0.json˝
?/app/src/monolith-service/features/workflow/dto/workflow.dto.cs§namespace backend.src.features.workflow.dto;

public class CreateWorkflowDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = "[]";
}

public class UpdateWorkflowDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? DefinitionJson { get; set; }
    public bool? IsActive { get; set; }
}

public class WorkflowResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = "[]";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
ParseOptions.0.jsonÁ
E/app/src/monolith-service/features/workflow/entity/workflow.entity.csàusing System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.src.features.workflow.entity
{
    [Table("workflows")]
    public class Workflow
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// JSON blob defining the sequence of nodes.
        /// Example: [{"type": "extractor"}, {"type": "search"}]
        /// </summary>
        [Required]
        [Column(TypeName = "jsonb")]
        public string DefinitionJson { get; set; } = "[]";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
ParseOptions.0.jsonπ
N/app/src/monolith-service/features/workflow/interfaces/iworkflow.repository.cs—using backend.src.features.workflow.entity;

namespace backend.src.features.workflow.interfaces;

public interface IWorkflowRepository
{
    Task<List<Workflow>> GetAll();
    Task<Workflow> GetById(Guid id);
    Task<Workflow> Create(Workflow entity);
    Task<Workflow> Update(Workflow entity);
    Task Delete(Guid id);
}
ParseOptions.0.jsonÒ
K/app/src/monolith-service/features/workflow/interfaces/iworkflow.service.csåusing backend.src.features.workflow.dto;

namespace backend.src.features.workflow.interfaces;

public interface IWorkflowService
{
    Task<List<WorkflowResponseDto>> GetAll();
    Task<WorkflowResponseDto> GetById(Guid id);
    Task<WorkflowResponseDto> Create(CreateWorkflowDto dto);
    Task<WorkflowResponseDto> Update(Guid id, UpdateWorkflowDto dto);
    Task Delete(Guid id);
}
ParseOptions.0.json
M/app/src/monolith-service/features/workflow/repository/workflow.repository.csâusing Microsoft.EntityFrameworkCore;
using backend.src.features.workflow.entity;
using backend.src.features.workflow.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.workflow.repository;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly AppDbContext _context;

    public WorkflowRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Workflow>> GetAll()
    {
        return await _context.Set<Workflow>().ToListAsync();
    }

    public async Task<Workflow> GetById(Guid id)
    {
        var entity = await _context.Set<Workflow>().FindAsync(id);
        
        if (entity == null)
            throw new NotFoundException($"Workflow with ID {id} not found");
            
        return entity;
    }

    public async Task<Workflow> Create(Workflow entity)
    {
        _context.Set<Workflow>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Workflow> Update(Workflow entity)
    {
        _context.Set<Workflow>().Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(Guid id)
    {
        var entity = await GetById(id);

        _context.Set<Workflow>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
ParseOptions.0.jsonÎ
G/app/src/monolith-service/features/workflow/service/workflow.service.csäusing backend.src.features.workflow.interfaces;
using backend.src.features.workflow.entity;
using backend.src.features.workflow.dto;
using AutoMapper;

namespace backend.src.features.workflow.service;

public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _repository;
    private readonly IMapper _mapper;

    public WorkflowService(IWorkflowRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<WorkflowResponseDto>> GetAll()
    {
        var entities = await _repository.GetAll();
        return _mapper.Map<List<WorkflowResponseDto>>(entities);
    }

    public async Task<WorkflowResponseDto> GetById(Guid id)
    {
        var entity = await _repository.GetById(id);
        return _mapper.Map<WorkflowResponseDto>(entity);
    }

    public async Task<WorkflowResponseDto> Create(CreateWorkflowDto dto)
    {
        var entity = _mapper.Map<Workflow>(dto);
        var created = await _repository.Create(entity);
        return _mapper.Map<WorkflowResponseDto>(created);
    }

    public async Task<WorkflowResponseDto> Update(Guid id, UpdateWorkflowDto dto)
    {
        var entity = await _repository.GetById(id);
        _mapper.Map(dto, entity);
        var updated = await _repository.Update(entity);
        return _mapper.Map<WorkflowResponseDto>(updated);
    }

    public async Task Delete(Guid id)
    {
        await _repository.Delete(id);
    }
}
ParseOptions.0.jsonÚ
>/app/src/monolith-service/features/workflow/workflow.mapper.csöusing AutoMapper;
using backend.src.features.workflow.entity;
using backend.src.features.workflow.dto;

namespace backend.src.features.workflow;

public class WorkflowMappingProfile : Profile
{
    public WorkflowMappingProfile()
    {
        CreateMap<Workflow, WorkflowResponseDto>();
        CreateMap<CreateWorkflowDto, Workflow>();
        CreateMap<UpdateWorkflowDto, Workflow>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}
ParseOptions.0.jsonˇ
>/app/src/monolith-service/features/workflow/workflow.module.csßusing Microsoft.Extensions.DependencyInjection;
using backend.src.features.workflow.interfaces;
using backend.src.features.workflow.service;
using backend.src.features.workflow.repository;

namespace backend.src.features.workflow;

public static class WorkflowModule
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();

        return services;
    }
}
ParseOptions.0.json 
A/app/src/monolith-service/infrastructure/database/AppDbContext.csÔusing Microsoft.EntityFrameworkCore;
using backend.src.features.user.entity;
using backend.src.features.project.entity;
using backend.src.features.skill.entity;
using backend.src.features.experience.entity;
using backend.src.features.workflow.entity;
using Pgvector;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Project> Projects { get; set;}
    public DbSet<Experience> Experiences { get; set;}
    public DbSet<Skill> Skills { get; set;}
    public DbSet<Workflow> Workflows { get; set;}

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure pgvector type
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();

        // Configure vector columns for RAG search
        modelBuilder.Entity<Experience>()
            .Property(e => e.DescriptionEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Project>()
            .Property(p => p.DescriptionEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Skill>()
            .Property(s => s.NameEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Project>()
            .HasOne(p => p.User)
            .WithMany(u => u.Projects)
            .HasForeignKey(p => p.UserId);

        modelBuilder.Entity<Skill>()
            .HasOne(s => s.User)
            .WithMany(u => u.Skills)
            .HasForeignKey(s => s.UserId);

        modelBuilder.Entity<Experience>()
            .HasOne(e => e.User)
            .WithMany(u => u.Experiences)
            .HasForeignKey(e => e.UserId);
    }
}ParseOptions.0.json‰

H/app/src/monolith-service/infrastructure/database/AppDbContextFactory.csÇ
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using DotNetEnv;

namespace backend.src.infrastructure.database
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            Env.Load();
            // 1. Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory) // make sure it finds appsettings.json
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // 2. Read connection string
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") 
                                    ?? configuration.GetConnectionString("DefaultConnection");

            // 3. Build DbContext options
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
ParseOptions.0.jsonê
</app/src/monolith-service/middleware/exception.middleware.cs∫using System.Net;
using System.Text.Json;
using backend.src.shared.responses;
using backend.src.shared.exceptions;
using Microsoft.Extensions.Logging;

namespace backend.src.middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception caught in middleware");
            await HandleException(context, ex);
        }
    }

    private static Task HandleException(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        int statusCode = StatusCodes.Status500InternalServerError;
        string message = "Internal server error";
        object? errors = null;

        if (exception is BaseApiException apiException)
        {
            statusCode = (int)apiException.StatusCode;
            message = apiException.Message;
            errors = apiException.Errors;
        }

        context.Response.StatusCode = statusCode;

        var response = ApiResponse<object>.ErrorResponse(message, errors);
        var json = JsonSerializer.Serialize(response);

        return context.Response.WriteAsync(json);
    }
}ParseOptions.0.json˘a
D/app/src/monolith-service/Migrations/20260314094618_initialCreate.csõausing System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class initialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AiProfileDataJson = table.Column<string>(type: "text", nullable: true),
                    PreferencesJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "experiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Company = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiSummaryJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experiences_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoginAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginAttempts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Used = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Role = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Achievements = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RepositoryUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DemoUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillsJson = table.Column<string>(type: "text", nullable: true),
                    AiSummaryJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_skills_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_experiences_UserId",
                table: "experiences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_UserId",
                table: "LoginAttempts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId",
                table: "PasswordResetTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_UserId",
                table: "projects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_skills_UserId",
                table: "skills",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_UserId",
                table: "UserTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experiences");

            migrationBuilder.DropTable(
                name: "LoginAttempts");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
ParseOptions.0.jsonêt
M/app/src/monolith-service/Migrations/20260314094618_initialCreate.Designer.cs©s// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260314094618_initialCreate")]
    partial class initialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("backend.src.features.auth.entity.LoginAttempt", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("AttemptedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("IpAddress")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("Success")
                        .HasColumnType("boolean");

                    b.Property<string>("UserAgent")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("LoginAttempts");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.PasswordResetToken", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("ExpiryDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<bool>("Used")
                        .HasColumnType("boolean");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("PasswordResetTokens");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.UserToken", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("ExpiryDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsRevoked")
                        .HasColumnType("boolean");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("UserTokens");
                });

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("Company")
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ReferenceUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("experiences");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Achievements")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("DemoUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("RepositoryUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Role")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("SkillsJson")
                        .HasColumnType("text");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("projects");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Category")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Level")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<int?>("YearsOfExperience")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("skills");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiProfileDataJson")
                        .HasColumnType("text");

                    b.Property<string>("AvatarUrl")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<DateTime?>("BirthDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("LastLogin")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("PreferencesJson")
                        .HasColumnType("text");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("users");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.LoginAttempt", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("LoginAttempts")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.PasswordResetToken", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("PasswordResetTokens")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.UserToken", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Tokens")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Experiences")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Projects")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Skills")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Navigation("Experiences");

                    b.Navigation("LoginAttempts");

                    b.Navigation("PasswordResetTokens");

                    b.Navigation("Projects");

                    b.Navigation("Skills");

                    b.Navigation("Tokens");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.json¡
B/app/src/monolith-service/Migrations/20260319182525_AddPgVector.csÂusing Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPgVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "NameEmbedding",
                table: "skills",
                type: "vector(384)",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "DescriptionEmbedding",
                table: "projects",
                type: "vector(384)",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "DescriptionEmbedding",
                table: "experiences",
                type: "vector(384)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEmbedding",
                table: "skills");

            migrationBuilder.DropColumn(
                name: "DescriptionEmbedding",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "DescriptionEmbedding",
                table: "experiences");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
ParseOptions.0.json⁄w
K/app/src/monolith-service/Migrations/20260319182525_AddPgVector.Designer.csıv// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace backend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260319182525_AddPgVector")]
    partial class AddPgVector
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("backend.src.features.auth.entity.LoginAttempt", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("AttemptedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("IpAddress")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("Success")
                        .HasColumnType("boolean");

                    b.Property<string>("UserAgent")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("LoginAttempts");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.PasswordResetToken", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("ExpiryDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<bool>("Used")
                        .HasColumnType("boolean");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("PasswordResetTokens");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.UserToken", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("ExpiryDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsRevoked")
                        .HasColumnType("boolean");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("UserTokens");
                });

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("Company")
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ReferenceUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("experiences");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Achievements")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("DemoUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("RepositoryUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Role")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("SkillsJson")
                        .HasColumnType("text");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("projects");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Category")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Level")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Vector>("NameEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<int?>("YearsOfExperience")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("skills");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiProfileDataJson")
                        .HasColumnType("text");

                    b.Property<string>("AvatarUrl")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<DateTime?>("BirthDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("LastLogin")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("PreferencesJson")
                        .HasColumnType("text");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("users");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.LoginAttempt", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("LoginAttempts")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.PasswordResetToken", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("PasswordResetTokens")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.auth.entity.UserToken", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Tokens")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Experiences")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Projects")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Skills")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Navigation("Experiences");

                    b.Navigation("LoginAttempts");

                    b.Navigation("PasswordResetTokens");

                    b.Navigation("Projects");

                    b.Navigation("Skills");

                    b.Navigation("Tokens");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.json£,
B/app/src/monolith-service/Migrations/20260321121139_AddKeycloak.cs«+using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddKeycloak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginAttempts");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "KeycloakId",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeycloakId",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "LoginAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginAttempts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Used = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_UserId",
                table: "LoginAttempts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId",
                table: "PasswordResetTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_UserId",
                table: "UserTokens",
                column: "UserId");
        }
    }
}
ParseOptions.0.jsonßS
K/app/src/monolith-service/Migrations/20260321121139_AddKeycloak.Designer.cs¬R// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace backend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260321121139_AddKeycloak")]
    partial class AddKeycloak
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("Company")
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ReferenceUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("experiences");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Achievements")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("DemoUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("RepositoryUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Role")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("SkillsJson")
                        .HasColumnType("text");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("projects");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Category")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Level")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Vector>("NameEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<int?>("YearsOfExperience")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("skills");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiProfileDataJson")
                        .HasColumnType("text");

                    b.Property<string>("AvatarUrl")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<DateTime?>("BirthDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("KeycloakId")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<DateTime?>("LastLogin")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("PreferencesJson")
                        .HasColumnType("text");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("users");
                });

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Experiences")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Projects")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Skills")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Navigation("Experiences");

                    b.Navigation("Projects");

                    b.Navigation("Skills");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.json¸
D/app/src/monolith-service/Migrations/20260401190831_AddVectorType.csûusing System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflows");
        }
    }
}
ParseOptions.0.json£\
M/app/src/monolith-service/Migrations/20260401190831_AddVectorType.Designer.csº[// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace backend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260401190831_AddVectorType")]
    partial class AddVectorType
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("Company")
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ReferenceUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("experiences");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Achievements")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("DemoUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("RepositoryUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Role")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("SkillsJson")
                        .HasColumnType("text");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("projects");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Category")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Level")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Vector>("NameEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<int?>("YearsOfExperience")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("skills");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiProfileDataJson")
                        .HasColumnType("text");

                    b.Property<string>("AvatarUrl")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<DateTime?>("BirthDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("KeycloakId")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<DateTime?>("LastLogin")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("PreferencesJson")
                        .HasColumnType("text");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("users");
                });

            modelBuilder.Entity("backend.src.features.workflow.entity.Workflow", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("DefinitionJson")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.ToTable("workflows");
                });

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Experiences")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Projects")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Skills")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Navigation("Experiences");

                    b.Navigation("Projects");

                    b.Navigation("Skills");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.jsonØ[
A/app/src/monolith-service/Migrations/AppDbContextModelSnapshot.cs‘Z// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace backend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("Company")
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ReferenceUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("experiences");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Achievements")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("DemoUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("RepositoryUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Role")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("SkillsJson")
                        .HasColumnType("text");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("projects");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Category")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Level")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Vector>("NameEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<int?>("YearsOfExperience")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("skills");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiProfileDataJson")
                        .HasColumnType("text");

                    b.Property<string>("AvatarUrl")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<DateTime?>("BirthDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("KeycloakId")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<DateTime?>("LastLogin")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("PreferencesJson")
                        .HasColumnType("text");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("users");
                });

            modelBuilder.Entity("backend.src.features.workflow.entity.Workflow", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("DefinitionJson")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.ToTable("workflows");
                });

            modelBuilder.Entity("backend.src.features.experience.entity.Experience", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Experiences")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.project.entity.Project", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Projects")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.skill.entity.Skill", b =>
                {
                    b.HasOne("backend.src.features.user.entity.User", "User")
                        .WithMany("Skills")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("backend.src.features.user.entity.User", b =>
                {
                    b.Navigation("Experiences");

                    b.Navigation("Projects");

                    b.Navigation("Skills");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.json∂D
$/app/src/monolith-service/Program.cs¯Cusing Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using backend.src.config;
using backend.src.shared.filters;
using backend.src.middleware;
using backend.src.features.user;
using backend.src.features.auth;
using backend.src.features.project;
using backend.src.features.skill;
using backend.src.features.experience;
using backend.src.features.workflow;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ------------------------
// 1. Configure services
// ------------------------

builder.Services.AddUserModule();
builder.Services.AddAuthModule();
builder.Services.AddProjectModule();
builder.Services.AddSkillModule();
builder.Services.AddExperienceModule();
builder.Services.AddWorkflowModule();

builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());

// Add controllers
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

// Add Swagger for API testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure EF Core with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector();
    var dataSource = dataSourceBuilder.Build();
     options.UseNpgsql(dataSource, o => o.UseVector());
});

// ------------------------
// 2. Configure Authentication
// ------------------------

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "auto";
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddPolicyScheme("auto", "Auto scheme selection (cookie or bearer)", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return JwtBearerDefaults.AuthenticationScheme;
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.None;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
    options.LoginPath = "/api/auth/login";
    options.AccessDeniedPath = "/api/auth/denied";
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = KeycloakConfig.Authority;
    options.MetadataAddress = KeycloakConfig.MetadataAddressWithDocker + "/.well-known/openid-configuration";
    options.ClientId = KeycloakConfig.ClientId;
    options.ClientSecret = KeycloakConfig.ClientSecret;
    options.ResponseType = KeycloakConfig.ResponseType;
    options.CallbackPath = KeycloakConfig.CallbackPath;
    options.RequireHttpsMetadata = KeycloakConfig.RequireHttpsMetadata;
    options.ResponseMode = "query";

    // Token handling
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.UsePkce = true;

    // Scopes
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("offline_access");

    // Claim mapping
    options.TokenValidationParameters.NameClaimType = "preferred_username";
    options.TokenValidationParameters.RoleClaimType = "realm_access.roles";

    // Events
    options.Events.OnTicketReceived = context =>
    {
        var scopeFactory = context.HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var principal = context.Principal;
        var keycloakId = principal?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(keycloakId))
        {
            context.Fail("No sub claim found from Keycloak");
            return Task.CompletedTask;
        }

        var user = dbContext.Users.FirstOrDefault(u => u.KeycloakId == keycloakId);
        if (user == null)
        {
            var email = principal?.FindFirst("email")?.Value;
            var username = principal?.FindFirst("preferred_username")?.Value;

            user = new backend.src.features.user.entity.User
            {
                KeycloakId = keycloakId,
                Email = email ?? $"{username}@keycloak.local",
                FirstName = principal?.FindFirst("given_name")?.Value ?? "Unknown",
                LastName = principal?.FindFirst("family_name")?.Value ?? "User",
                Role = backend.src.features.user.entity.Role.USER,
                CreatedAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
        }
        else
        {
            user.LastLogin = DateTime.UtcNow;
        }

        dbContext.SaveChanges();

        var identity = principal?.Identity as System.Security.Claims.ClaimsIdentity;
        identity?.AddClaim(new System.Security.Claims.Claim("local_user_id", user.Id.ToString()));

        return Task.CompletedTask;
    };

    options.Events.OnRemoteFailure = context =>
    {
        context.HandleResponse();
        context.Response.Redirect("/api/auth/error?message=" + Uri.EscapeDataString(context.Failure?.Message ?? "Unknown error"));
        return Task.CompletedTask;
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Authority = KeycloakConfig.Authority;
    options.MetadataAddress = KeycloakConfig.MetadataAddressWithDocker + "/.well-known/openid-configuration";
    options.RequireHttpsMetadata = KeycloakConfig.RequireHttpsMetadata;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = KeycloakConfig.ClientId,
        ValidIssuer = KeycloakConfig.Authority.TrimEnd('/'),
        RoleClaimType = "realm_access.roles",
        NameClaimType = "preferred_username"
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(context.Exception, "JWT authentication failed");
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

// ------------------------
// Run database migrations (only if needed)
// ------------------------
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

        if (pendingMigrations.Any())
        {
            logger.LogInformation("Found {Count} pending migrations. Applying...", pendingMigrations.Count());
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("No pending migrations found. Database is up to date.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying migrations.");
    }
}

// ------------------------
// 3. Configure middleware
// ------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable HTTPS
// app.UseHttpsRedirection();

app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello from backend!");
app.MapGet("/secure", [Authorize] () => "Hello from secure backend!");

app.MapControllers();

app.Run();
ParseOptions.0.json¯
@/app/src/monolith-service/shared/exceptions/baseApi.exception.csûusing System.Net;
namespace backend.src.shared.exceptions;

public class BaseApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public object? Errors { get; }

    public BaseApiException(
        string message,
        HttpStatusCode statusCode,
        object? errors = null
    ) : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
    }
}ParseOptions.0.jsonø
A/app/src/monolith-service/shared/exceptions/conflict.exception.cs‰namespace backend.src.shared.exceptions;

using System.Net;
public class ConflictException : BaseApiException
{
    public ConflictException(string message)
        : base(message, HttpStatusCode.Conflict)
    {
    }
}ParseOptions.0.json¡
A/app/src/monolith-service/shared/exceptions/notFound.exception.csÊusing System.Net;

namespace backend.src.shared.exceptions;

public class NotFoundException : BaseApiException
{
    public NotFoundException(string message)
        : base(message, HttpStatusCode.NotFound)
    {
    }
}ParseOptions.0.jsonË
C/app/src/monolith-service/shared/exceptions/validation.exception.csãusing System.Net;

namespace backend.src.shared.exceptions;

public class ValidationException : BaseApiException
{
    public ValidationException(string message, object? errors = null)
        : base(message, HttpStatusCode.BadRequest, errors)
    {
    }
}ParseOptions.0.json⁄
=/app/src/monolith-service/shared/filters/validation.filter.csÉusing Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using backend.src.shared.exceptions;

namespace backend.src.shared.filters;

public class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage)
                );

            throw new ValidationException("Validation failed", errors);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}ParseOptions.0.json±
:/app/src/monolith-service/shared/responses/api.response.cs›namespace backend.src.shared.responses;

public class ApiResponse<T>
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public T? Data { get; set; }

    public object? Errors { get; set; }

    public static ApiResponse<T> SuccessResponse(T? data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static ApiResponse<T> ErrorResponse(string message, object? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}ParseOptions.0.jsonÿ
M/app/src/monolith-service/obj/Debug/net10.0/MonolithService.GlobalUsings.g.csÒ// <auto-generated/>
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Net.Http.Json;
global using System.Threading;
global using System.Threading.Tasks;
ParseOptions.0.jsonπ
[/app/src/monolith-service/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.csƒ// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
ParseOptions.0.json°
K/app/src/monolith-service/obj/Debug/net10.0/MonolithService.AssemblyInfo.csº//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("MonolithService")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0")]
[assembly: System.Reflection.AssemblyProductAttribute("MonolithService")]
[assembly: System.Reflection.AssemblyTitleAttribute("MonolithService")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.jsonâ
^/app/src/monolith-service/obj/Debug/net10.0/MonolithService.MvcApplicationPartsAssemblyInfo.csë//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute("Microsoft.AspNetCore.OpenApi")]
[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute("Microsoft.Identity.Web")]
[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute("Swashbuckle.AspNetCore.SwaggerGen")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.jsonÉ
C/app/src/monolith-service/obj/Debug/net10.0/EFCoreNpgsqlPgvector.cs¶//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: Microsoft.EntityFrameworkCore.Design.DesignTimeServicesReferenceAttribute(("Pgvector.EntityFrameworkCore.VectorDesignTimeServices, Pgvector.EntityFrameworkCo" +
    "re"), "Npgsql.EntityFrameworkCore.PostgreSQL")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json†Œ
¡/app/src/monolith-service/obj/Debug/net10.0/Microsoft.AspNetCore.OpenApi.SourceGenerators/Microsoft.AspNetCore.OpenApi.SourceGenerators.XmlCommentGenerator/OpenApiXmlCommentSupport.generated.cs√Ã//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
// Suppress warnings about obsolete types and members
// in generated code
#pragma warning disable CS0612, CS0618

namespace System.Runtime.CompilerServices
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    file sealed class InterceptsLocationAttribute : System.Attribute
    {
        public InterceptsLocationAttribute(int version, string data)
        {
        }
    }
}

namespace Microsoft.AspNetCore.OpenApi.Generated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.OpenApi;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.OpenApi;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    file record XmlComment(
        string? Summary,
        string? Description,
        string? Remarks,
        string? Returns,
        string? Value,
        bool Deprecated,
        List<string>? Examples,
        List<XmlParameterComment>? Parameters,
        List<XmlResponseComment>? Responses);

    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    file record XmlParameterComment(string? Name, string? Description, string? Example, bool Deprecated);

    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    file record XmlResponseComment(string Code, string? Description, string? Example);

    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    file static class XmlCommentCache
    {
        private static Dictionary<string, XmlComment>? _cache;
        public static Dictionary<string, XmlComment> Cache => _cache ??= GenerateCacheEntries();

        private static Dictionary<string, XmlComment> GenerateCacheEntries()
        {
            var cache = new Dictionary<string, XmlComment>();



            return cache;
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    file static class DocumentationCommentIdHelper
    {
        /// <summary>
        /// Generates a documentation comment ID for a type.
        /// Example: T:Namespace.Outer+Inner`1 becomes T:Namespace.Outer.Inner`1
        /// </summary>
        public static string CreateDocumentationId(this Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return "T:" + GetTypeDocId(type, includeGenericArguments: false, omitGenericArity: false);
        }

        /// <summary>
        /// Generates a documentation comment ID for a property.
        /// Example: P:Namespace.ContainingType.PropertyName or for an indexer P:Namespace.ContainingType.Item(System.Int32)
        /// </summary>
        public static string CreateDocumentationId(this PropertyInfo property)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            var sb = new StringBuilder();
            sb.Append("P:");

            if (property.DeclaringType != null)
            {
                sb.Append(GetTypeDocId(property.DeclaringType, includeGenericArguments: false, omitGenericArity: false));
            }

            sb.Append('.');
            sb.Append(property.Name);

            // For indexers, include the parameter list.
            var indexParams = property.GetIndexParameters();
            if (indexParams.Length > 0)
            {
                sb.Append('(');
                for (int i = 0; i < indexParams.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(GetTypeDocId(indexParams[i].ParameterType, includeGenericArguments: true, omitGenericArity: false));
                }
                sb.Append(')');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a documentation comment ID for a property given its container type and property name.
        /// Example: P:Namespace.ContainingType.PropertyName
        /// </summary>
        public static string CreateDocumentationId(Type containerType, string propertyName)
        {
            if (containerType == null)
            {
                throw new ArgumentNullException(nameof(containerType));
            }
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("Property name cannot be null or empty.", nameof(propertyName));
            }

            var sb = new StringBuilder();
            sb.Append("P:");
            sb.Append(GetTypeDocId(containerType, includeGenericArguments: false, omitGenericArity: false));
            sb.Append('.');
            sb.Append(propertyName);

            return sb.ToString();
        }

        /// <summary>
        /// Generates a documentation comment ID for a method (or constructor).
        /// For example:
        ///   M:Namespace.ContainingType.MethodName(ParamType1,ParamType2)~ReturnType
        ///   M:Namespace.ContainingType.#ctor(ParamType)
        /// </summary>
        public static string CreateDocumentationId(this MethodInfo method)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            var sb = new StringBuilder();
            sb.Append("M:");

            // Append the fully qualified name of the declaring type.
            if (method.DeclaringType != null)
            {
                sb.Append(GetTypeDocId(method.DeclaringType, includeGenericArguments: false, omitGenericArity: false));
            }

            sb.Append('.');

            // Append the method name, handling constructors specially.
            if (method.IsConstructor)
            {
                sb.Append(method.IsStatic ? "#cctor" : "#ctor");
            }
            else
            {
                sb.Append(method.Name);
                if (method.IsGenericMethod)
                {
                    sb.Append("``");
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", method.GetGenericArguments().Length);
                }
            }

            // Append the parameter list, if any.
            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                sb.Append('(');
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    // Omit the generic arity for the parameter type.
                    sb.Append(GetTypeDocId(parameters[i].ParameterType, includeGenericArguments: true, omitGenericArity: true));
                }
                sb.Append(')');
            }

            // Append the return type after a '~' (if the method returns a value).
            if (method.ReturnType != typeof(void))
            {
                sb.Append('~');
                // Omit the generic arity for the return type.
                sb.Append(GetTypeDocId(method.ReturnType, includeGenericArguments: true, omitGenericArity: true));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a documentation ID string for a type.
        /// This method handles nested types (replacing '+' with '.'),
        /// generic types, arrays, pointers, by-ref types, and generic parameters.
        /// The <paramref name="includeGenericArguments"/> flag controls whether
        /// constructed generic type arguments are emitted, while <paramref name="omitGenericArity"/>
        /// controls whether the generic arity marker (e.g. "`1") is appended.
        /// </summary>
        private static string GetTypeDocId(Type type, bool includeGenericArguments, bool omitGenericArity)
        {
            if (type.IsGenericParameter)
            {
                // Use `` for method-level generic parameters and ` for type-level.
                if (type.DeclaringMethod != null)
                {
                    return "``" + type.GenericParameterPosition;
                }
                else if (type.DeclaringType != null)
                {
                    return "`" + type.GenericParameterPosition;
                }
                else
                {
                    return type.Name;
                }
            }

            if (type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                string fullName = genericDef.FullName ?? genericDef.Name;

                var sb = new StringBuilder(fullName.Length);

                // Replace '+' with '.' for nested types
                for (var i = 0; i < fullName.Length; i++)
                {
                    char c = fullName[i];
                    if (c == '+')
                    {
                        sb.Append('.');
                    }
                    else if (c == '`')
                    {
                        break;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                if (!omitGenericArity)
                {
                    int arity = genericDef.GetGenericArguments().Length;
                    sb.Append('`');
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", arity);
                }

                if (includeGenericArguments && !type.IsGenericTypeDefinition)
                {
                    var typeArgs = type.GetGenericArguments();
                    sb.Append('{');

                    for (int i = 0; i < typeArgs.Length; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(',');
                        }

                        sb.Append(GetTypeDocId(typeArgs[i], includeGenericArguments, omitGenericArity));
                    }

                    sb.Append('}');
                }

                return sb.ToString();
            }

            // For non-generic types, use FullName (if available) and replace nested type separators.
            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        /// <summary>
        /// Normalizes a documentation comment ID to match the compiler-style format.
        /// Strips the return type suffix for ordinary methods but retains it for conversion operators.
        /// </summary>
        /// <param name="docId">The documentation comment ID to normalize.</param>
        /// <returns>The normalized documentation comment ID.</returns>
        public static string NormalizeDocId(string docId)
        {
            // Find the tilde character that indicates the return type suffix
            var tildeIndex = docId.IndexOf('~');
            if (tildeIndex == -1)
            {
                // No return type suffix, return as-is
                return docId;
            }

            // Check if this is a conversion operator (op_Implicit or op_Explicit)
            // For these operators, we need to keep the return type suffix
            if (docId.Contains("op_Implicit") || docId.Contains("op_Explicit"))
            {
                return docId;
            }

            // For ordinary methods, strip the return type suffix
            return docId.Substring(0, tildeIndex);
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    file class XmlCommentOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            var methodInfo = context.Description.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor
                ? controllerActionDescriptor.MethodInfo
                : context.Description.ActionDescriptor.EndpointMetadata.OfType<MethodInfo>().SingleOrDefault();

            if (methodInfo is null)
            {
                return Task.CompletedTask;
            }
            if (XmlCommentCache.Cache.TryGetValue(DocumentationCommentIdHelper.NormalizeDocId(methodInfo.CreateDocumentationId()), out var methodComment))
            {
                if (methodComment.Summary is { } summary)
                {
                    operation.Summary = summary;
                }
                if (methodComment.Description is { } description)
                {
                    operation.Description = description;
                }
                if (methodComment.Remarks is { } remarks)
                {
                    operation.Description = remarks;
                }
                if (methodComment.Parameters is { Count: > 0})
                {
                    foreach (var parameterComment in methodComment.Parameters)
                    {
                        var parameterInfo = methodInfo.GetParameters().SingleOrDefault(info => info.Name == parameterComment.Name);
                        var operationParameter = operation.Parameters?.SingleOrDefault(parameter => parameter.Name == parameterComment.Name);
                        if (operationParameter is not null)
                        {
                            var targetOperationParameter = UnwrapOpenApiParameter(operationParameter);
                            targetOperationParameter.Description = parameterComment.Description;
                            if (parameterComment.Example is { } jsonString)
                            {
                                targetOperationParameter.Example = jsonString.Parse();
                            }
                            targetOperationParameter.Deprecated = parameterComment.Deprecated;
                        }
                        else
                        {
                            var requestBody = operation.RequestBody;
                            if (requestBody is not null)
                            {
                                requestBody.Description = parameterComment.Description;
                                if (parameterComment.Example is { } jsonString)
                                {
                                    var content = requestBody?.Content?.Values;
                                    if (content is null)
                                    {
                                        continue;
                                    }
                                    foreach (var mediaType in content)
                                    {
                                        mediaType.Example = jsonString.Parse();
                                    }
                                }
                            }
                        }
                    }
                }
                // Applies `<returns>` on XML comments for operation with single response value.
                if (methodComment.Returns is { } returns && operation.Responses is { Count: 1 })
                {
                    var response = operation.Responses.First();
                    response.Value.Description = returns;
                }
                // Applies `<response>` on XML comments for operation with multiple response values.
                if (methodComment.Responses is { Count: > 0} && operation.Responses is { Count: > 0 })
                {
                    foreach (var response in operation.Responses)
                    {
                        var responseComment = methodComment.Responses.SingleOrDefault(xmlResponse => xmlResponse.Code == response.Key);
                        if (responseComment is not null)
                        {
                            response.Value.Description = responseComment.Description;
                        }
                    }
                }
            }
            foreach (var parameterDescription in context.Description.ParameterDescriptions)
            {
                var metadata = parameterDescription.ModelMetadata;
                if (metadata is not null
                    && metadata.MetadataKind == ModelMetadataKind.Property
                    && metadata.ContainerType is { } containerType
                    && metadata.PropertyName is { } propertyName)
                {
                    var propertyDocId = DocumentationCommentIdHelper.CreateDocumentationId(containerType, propertyName);
                    if (XmlCommentCache.Cache.TryGetValue(DocumentationCommentIdHelper.NormalizeDocId(propertyDocId), out var propertyComment))
                    {
                        var parameter = operation.Parameters?.SingleOrDefault(p => p.Name == metadata.Name);
                        var description = propertyComment.Summary;
                        if (!string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(propertyComment.Value))
                        {
                            description = $"{description}\n{propertyComment.Value}";
                        }
                        else if (string.IsNullOrEmpty(description))
                        {
                            description = propertyComment.Value;
                        }
                        if (parameter is null)
                        {
                            if (operation.RequestBody is not null)
                            {
                                operation.RequestBody.Description = description;
                                if (propertyComment.Examples?.FirstOrDefault() is { } jsonString)
                                {
                                    var content = operation.RequestBody.Content?.Values;
                                    if (content is null)
                                    {
                                        continue;
                                    }
                                    var parsedExample = jsonString.Parse();
                                    foreach (var mediaType in content)
                                    {
                                        mediaType.Example = parsedExample;
                                    }
                                }
                            }
                            continue;
                        }
                        var targetOperationParameter = UnwrapOpenApiParameter(parameter);
                        if (targetOperationParameter is not null)
                        {
                            targetOperationParameter.Description = description;
                            if (propertyComment.Examples?.FirstOrDefault() is { } jsonString)
                            {
                                targetOperationParameter.Example = jsonString.Parse();
                            }
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        private static OpenApiParameter UnwrapOpenApiParameter(IOpenApiParameter sourceParameter)
        {
            if (sourceParameter is OpenApiParameterReference parameterReference)
            {
                if (parameterReference.Target is OpenApiParameter target)
                {
                    return target;
                }
                else
                {
                    throw new InvalidOperationException($"The input schema must be an {nameof(OpenApiParameter)} or {nameof(OpenApiParameterReference)}.");
                }
            }
            else if (sourceParameter is OpenApiParameter directParameter)
            {
                return directParameter;
            }
            else
            {
                throw new InvalidOperationException($"The input schema must be an {nameof(OpenApiParameter)} or {nameof(OpenApiParameterReference)}.");
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    file class XmlCommentSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            // Apply comments from the type
            if (XmlCommentCache.Cache.TryGetValue(DocumentationCommentIdHelper.NormalizeDocId(context.JsonTypeInfo.Type.CreateDocumentationId()), out var typeComment))
            {
                schema.Description = typeComment.Summary;
                if (typeComment.Examples?.FirstOrDefault() is { } jsonString)
                {
                    schema.Example = jsonString.Parse();
                }
            }

            if (context.JsonPropertyInfo is { AttributeProvider: PropertyInfo propertyInfo })
            {
                // Apply comments from the property
                if (XmlCommentCache.Cache.TryGetValue(DocumentationCommentIdHelper.NormalizeDocId(propertyInfo.CreateDocumentationId()), out var propertyComment))
                {
                    var description = propertyComment.Summary;
                    if (!string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(propertyComment.Value))
                    {
                        description = $"{description}\n{propertyComment.Value}";
                    }
                    else if (string.IsNullOrEmpty(description))
                    {
                        description = propertyComment.Value;
                    }
                    if (schema.Metadata is null
                        || !schema.Metadata.TryGetValue("x-schema-id", out var schemaId)
                        || string.IsNullOrEmpty(schemaId as string))
                    {
                        // Inlined schema
                        schema.Description = description;
                        if (propertyComment.Examples?.FirstOrDefault() is { } jsonString)
                        {
                            schema.Example = jsonString.Parse();
                        }
                    }
                    else
                    {
                        // Schema Reference
                        if (!string.IsNullOrEmpty(description))
                        {
                            schema.Metadata["x-ref-description"] = description;
                        }
                        if (propertyComment.Examples?.FirstOrDefault() is { } jsonString)
                        {
                            schema.Metadata["x-ref-example"] = jsonString.Parse()!;
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    file static class JsonNodeExtensions
    {
        public static JsonNode? Parse(this string? json)
        {
            if (json is null)
            {
                return null;
            }

            try
            {
                return JsonNode.Parse(json);
            }
            catch (JsonException)
            {
                try
                {
                    // If parsing fails, try wrapping in quotes to make it a valid JSON string
                    return JsonNode.Parse($"\"{json.Replace("\"", "\\\"")}\"");
                }
                catch (JsonException)
                {
                    return null;
                }
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.AspNetCore.OpenApi.SourceGenerators, Version=10.0.3.0, Culture=neutral, PublicKeyToken=adb9793829ddae60", "10.0.3.0")]
    file static class GeneratedServiceCollectionExtensions
    {

    }
}
ParseOptions.0.json˝
Ω/app/src/monolith-service/obj/Debug/net10.0/Microsoft.AspNetCore.App.SourceGenerators/Microsoft.AspNetCore.SourceGenerators.PublicProgramSourceGenerator/PublicTopLevelProgram.Generated.g.cs•// <auto-generated />
/// <summary>
/// Auto-generated public partial Program class for top-level statement apps.
/// </summary>
public partial class Program { }ParseOptions.0.json