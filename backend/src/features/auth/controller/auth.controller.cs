using Microsoft.AspNetCore.Authentication;
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
