using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

var keycloakInternalUrl = Environment.GetEnvironmentVariable("KEYCLOAK_INTERNAL_URL") ?? "http://localhost:9090";
var keycloakExternalUrl = Environment.GetEnvironmentVariable("KEYCLOAK_EXTERNAL_URL") ?? "http://localhost:9090";
var keycloakRealm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM") ?? "cv-realm";
var keycloakClientId = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_ID") ?? "cv-gateway";
var keycloakClientSecret = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_SECRET") ?? "change-me-in-production";
var gatewayUrl = Environment.GetEnvironmentVariable("GATEWAY_URL") ?? "http://localhost:8080";

var authority = $"{keycloakInternalUrl}/realms/{keycloakRealm}";
var keycloakLoginUrl = $"{keycloakExternalUrl}/realms/{keycloakRealm}/protocol/openid-connect/auth";
var keycloakLogoutUrl = $"{keycloakExternalUrl}/realms/{keycloakRealm}/protocol/openid-connect/logout";

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ── Authentication: Cookies + JWT ────────────────────────────────────────────
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "cv_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = true;
});

// ── JWT Bearer for downstream services ───────────────────────────────────────
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
        };
    });

// 2. Define the "default" authorization policy that we reference in appsettings.json
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("default", policy => policy.RequireAuthenticatedUser());
});

// ── Reverse Proxy ────────────────────────────────────────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("Proxy"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── HTTP Client for token exchange ───────────────────────────────────────────
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseCors();

app.UseSwagger();
app.UseSwaggerUI();

// ── Auth Endpoints ───────────────────────────────────────────────────────────
app.MapGet("/api/auth/login", async (HttpContext ctx, string? returnUrl) =>
{
    var state = Guid.NewGuid().ToString("N");
    var nonce = Guid.NewGuid().ToString("N");
    var redirectUri = $"{gatewayUrl}/api/auth/callback";
    var scope = Uri.EscapeDataString("openid profile email");
    var encodedState = Uri.EscapeDataString(state);
    var encodedNonce = Uri.EscapeDataString(nonce);
    var encodedReturn = Uri.EscapeDataString(returnUrl ?? gatewayUrl);

    var loginUrl = $"{keycloakLoginUrl}"
        + $"?client_id={keycloakClientId}"
        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
        + $"&response_type=code"
        + $"&scope={scope}"
        + $"&state={encodedState}:{encodedReturn}"
        + $"&nonce={encodedNonce}";

    ctx.Response.Cookies.Append("oidc_state", state, new CookieOptions
    {
        HttpOnly = true,
        Secure = false,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddMinutes(10),
    });
    ctx.Response.Cookies.Append("oidc_nonce", nonce, new CookieOptions
    {
        HttpOnly = true,
        Secure = false,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddMinutes(10),
    });

    ctx.Response.Redirect(loginUrl);
})
.RequireCors("Default");

app.MapGet("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    var logoutUrl = $"{keycloakLogoutUrl}"
        + $"?post_logout_redirect_uri={Uri.EscapeDataString("http://localhost:4200/login")}";

    ctx.Response.Redirect(logoutUrl);
})
.RequireCors("Default");

app.MapGet("/api/auth/callback", async (HttpContext ctx, IHttpClientFactory httpClientFactory) =>
{
    var code = ctx.Request.Query["code"].ToString();
    var stateParam = ctx.Request.Query["state"].ToString();
    var error = ctx.Request.Query["error"].ToString();

    if (!string.IsNullOrEmpty(error))
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new { error, error_description = ctx.Request.Query["error_description"] });
        return;
    }

    if (string.IsNullOrEmpty(code))
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new { error = "missing_code" });
        return;
    }

    var storedState = ctx.Request.Cookies["oidc_state"];
    var expectedReturn = gatewayUrl;

    if (!string.IsNullOrEmpty(stateParam) && !string.IsNullOrEmpty(storedState))
    {
        if (stateParam.StartsWith(storedState + ":"))
        {
            var returnPart = stateParam.Substring(storedState.Length + 1);
            expectedReturn = Uri.UnescapeDataString(returnPart);
        }
    }

    ctx.Response.Cookies.Delete("oidc_state");
    ctx.Response.Cookies.Delete("oidc_nonce");

    var tokenUrl = $"{keycloakInternalUrl}/realms/{keycloakRealm}/protocol/openid-connect/token";
    var redirectUri = $"{gatewayUrl}/api/auth/callback";

    var http = httpClientFactory.CreateClient();
    var content = new FormUrlEncodedContent(new[]
    {
        new KeyValuePair<string, string>("grant_type", "authorization_code"),
        new KeyValuePair<string, string>("code", code),
        new KeyValuePair<string, string>("redirect_uri", redirectUri),
        new KeyValuePair<string, string>("client_id", keycloakClientId),
        new KeyValuePair<string, string>("client_secret", keycloakClientSecret),
    });

    var tokenResponse = await http.PostAsync(tokenUrl, content);
    if (!tokenResponse.IsSuccessStatusCode)
    {
        ctx.Response.StatusCode = 502;
        await ctx.Response.WriteAsJsonAsync(new { error = "token_exchange_failed" });
        return;
    }

    var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    if (tokenJson == null)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { error = "invalid_token_response" });
        return;
    }

    var accessToken = tokenJson.GetValueOrDefault("access_token")?.ToString() ?? "";
    var idToken = tokenJson.GetValueOrDefault("id_token")?.ToString() ?? "";
    var refreshToken = tokenJson.GetValueOrDefault("refresh_token")?.ToString() ?? "";

    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(idToken);
    var claims = jwt.Claims.ToList();

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    identity.AddClaim(new Claim("access_token", accessToken));
    if (!string.IsNullOrEmpty(refreshToken))
        identity.AddClaim(new Claim("refresh_token", refreshToken));
    var principal = new ClaimsPrincipal(identity);

    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
    {
        IsPersistent = true,
        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        RedirectUri = expectedReturn,
    });

    ctx.Items["access_token"] = accessToken;
    ctx.Items["refresh_token"] = refreshToken;

    ctx.Response.Redirect(expectedReturn);
})
.RequireCors("Default");

app.MapGet("/api/auth/me", async (HttpContext ctx) =>
{
    var result = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    if (!result.Succeeded || result.Principal?.Identity?.IsAuthenticated != true)
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsJsonAsync(new { authenticated = false });
        return;
    }

    var accessToken = result.Principal.FindFirstValue("access_token") ?? "";
    var userId = result.Principal.FindFirstValue("userId")
        ?? result.Principal.FindFirstValue("sub")
        ?? "";
    var keycloakId = result.Principal.FindFirstValue("sub") ?? "";
    var firstName = result.Principal.FindFirstValue("given_name") ?? "";
    var lastName = result.Principal.FindFirstValue("family_name") ?? "";
    var email = result.Principal.FindFirstValue("email") ?? "";
    var role = result.Principal.FindFirstValue("roles") ?? "user";

    await ctx.Response.WriteAsJsonAsync(new
    {
        success = true,
        data = new
        {
            userId,
            keycloakId,
            firstName,
            lastName,
            email,
            role,
            isActive = true,
            tokens = new
            {
                accessToken,
                hasRefreshToken = !string.IsNullOrEmpty(result.Principal.FindFirstValue("refresh_token")),
            },
        },
    });
})
.RequireCors("Default");

app.MapPost("/api/auth/register", async (HttpContext ctx) =>
{
    ctx.Response.StatusCode = 501;
    await ctx.Response.WriteAsJsonAsync(new
    {
        success = false,
        message = "Registration through Keycloak admin console or self-registration (not yet implemented)",
    });
})
.RequireCors("Default");

// ── Proxy with token forwarding ──────────────────────────────────────────────
app.MapReverseProxy(proxyApp =>
{
    proxyApp.Use(async (ctx, next) =>
    {
        var authResult = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (authResult.Succeeded)
        {
            var token = authResult.Principal?.FindFirstValue("access_token");
            if (!string.IsNullOrEmpty(token))
            {
                ctx.Request.Headers.Authorization = $"Bearer {token}";
            }
        }
        await next();
    });
});

app.Run();
