using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator;
using CV_Generator.Models;
using CV_Generator.Data;
using CV_Generator.Services;
using CV_Generator.Dto;

namespace CV_Generator.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<UsersController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public UsersController(AppDbContext db, IEventBus eventBus, ILogger<UsersController> logger, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        _scopeFactory = scopeFactory;
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

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var keycloakId = User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized(ApiResponse<UserResponseDto>.Error("Invalid token"));

        var email = User.FindFirstValue("email") ?? "";
        var firstName = User.FindFirstValue("given_name") ?? "";
        var lastName = User.FindFirstValue("family_name") ?? "";

        var (user, created) = await GetOrCreateUserAsync(keycloakId, email, firstName, lastName);
        await _db.SaveChangesAsync();

        if (created)
        {
            _logger.LogInformation("Created user {Id} from JWT (sub={KeycloakId})", user.Id, keycloakId);
            await _eventBus.PublishAsync(new UserCreatedEvent(user.Id, user.Email, user.FirstName, user.LastName));
        }

        return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
    }

    [HttpPost("sync")]
    [Authorize]
    public async Task<IActionResult> Sync()
    {
        var keycloakId = User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized(ApiResponse<UserResponseDto>.Error("Invalid token"));

        var email = User.FindFirstValue("email") ?? "";
        var firstName = User.FindFirstValue("given_name") ?? "";
        var lastName = User.FindFirstValue("family_name") ?? "";

        var (user, created) = await GetOrCreateUserAsync(keycloakId, email, firstName, lastName);
        await _db.SaveChangesAsync();

        if (created)
        {
            _logger.LogInformation("Created user {Id} via sync", user.Id);
            await _eventBus.PublishAsync(new UserCreatedEvent(user.Id, user.Email, user.FirstName, user.LastName));
        }

        return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
    }

    /// <summary>
    /// Returns the current user's local row for a Keycloak identity, creating it when needed.
    /// If the row exists by email but its Keycloak sub changed (e.g. a Keycloak account was
    /// recreated with the same email), the existing row is adopted — updating the sub — instead
    /// of inserting a duplicate (which would violate the unique IX_users_Email index).
    /// </summary>
    private async Task<(User User, bool Created)> GetOrCreateUserAsync(string keycloakId, string email, string firstName, string lastName)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        if (user != null)
        {
            RefreshIdentity(user, keycloakId, email, firstName, lastName);
            return (user, false);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower());
            if (byEmail != null)
            {
                _logger.LogWarning("Adopting user {Id} (email) for new Keycloak sub={KeycloakId}", byEmail.Id, keycloakId);
                RefreshIdentity(byEmail, keycloakId, email, firstName, lastName);
                return (byEmail, false);
            }
        }

        user = new User
        {
            KeycloakId = keycloakId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Role = Role.USER,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        _db.Users.Add(user);

        return (user, true);
    }

    private static void RefreshIdentity(User user, string keycloakId, string email, string firstName, string lastName)
    {
        user.KeycloakId = keycloakId;
        if (!string.IsNullOrWhiteSpace(email)) user.Email = email.Trim();
        if (!string.IsNullOrWhiteSpace(firstName)) user.FirstName = firstName;
        if (!string.IsNullOrWhiteSpace(lastName)) user.LastName = lastName;
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
        SearchSyncHelper.TriggerSync(_scopeFactory, user.Id, _logger, "User.Create");

        _logger.LogInformation("Created user {Id}", user.Id);

        await _eventBus.PublishAsync(new UserCreatedEvent(user.Id, user.Email, user.FirstName, user.LastName));

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
        if (dto.BirthDate != null && DateTime.TryParse(dto.BirthDate, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var birthDate))
            user.BirthDate = DateTime.SpecifyKind(birthDate, DateTimeKind.Utc);
        user.AvatarUrl = dto.AvatarUrl;
        user.PreferencesJson = dto.PreferencesJson;
        user.Headline = dto.Headline;
        user.Bio = dto.Bio;
        user.City = dto.City;
        user.Country = dto.Country;
        user.AuthorizedCountry = dto.AuthorizedCountry;
        user.RequiresVisaSponsorship = dto.RequiresVisaSponsorship;
        user.NoticePeriod = dto.NoticePeriod;
        user.EmploymentTypes = dto.EmploymentTypes;
        user.RemotePreference = dto.RemotePreference;
        user.WillingToRelocate = dto.WillingToRelocate;
        user.DesiredJobTitle = dto.DesiredJobTitle;
        user.DesiredSalaryMin = dto.DesiredSalaryMin;
        user.DesiredSalaryMax = dto.DesiredSalaryMax;
        user.ProfessionalTitles = dto.ProfessionalTitles;
        if (dto.ProfilePhotoKey != null) user.ProfilePhotoKey = string.IsNullOrWhiteSpace(dto.ProfilePhotoKey) ? null : dto.ProfilePhotoKey.Trim();

        await _db.SaveChangesAsync();
        SearchSyncHelper.TriggerSync(_scopeFactory, user.Id, _logger, "User.Update");
        return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(ApiResponse<object>.Error("User not found"));

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        SearchSyncHelper.TriggerSync(_scopeFactory, user.Id, _logger, "User.Delete");
        return NoContent();
    }

    private static UserResponseDto ToDto(User u) => new(
        u.Id, u.KeycloakId, u.FirstName, u.LastName, u.Email,
        u.PhoneNumber, u.BirthDate?.ToString("O"), u.Role.ToString(),
        u.AvatarUrl, u.CreatedAt, u.LastLogin, u.IsActive,
        u.AiProfileDataJson, u.PreferencesJson,
        u.Headline, u.Bio, u.City, u.Country, u.AuthorizedCountry,
        u.RequiresVisaSponsorship, u.NoticePeriod, u.EmploymentTypes,
        u.RemotePreference, u.WillingToRelocate, u.DesiredJobTitle,
        u.DesiredSalaryMin, u.DesiredSalaryMax, u.ProfessionalTitles,
        u.ProfilePhotoKey
    );
}
