namespace backend.src.features.auth.dto;

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
