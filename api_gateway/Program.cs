using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using Confluent.Kafka;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

var keycloakInternalUrl = Environment.GetEnvironmentVariable("KEYCLOAK_INTERNAL_URL") ?? "http://localhost:9090";
var keycloakExternalUrl = Environment.GetEnvironmentVariable("KEYCLOAK_EXTERNAL_URL") ?? "http://localhost:9090";
var keycloakRealm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM") ?? "cv-realm";
var keycloakClientId = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_ID") ?? "cv-gateway";
var keycloakClientSecret = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_SECRET") ?? "change-me-in-production";
var gatewayUrl = Environment.GetEnvironmentVariable("GATEWAY_URL") ?? "http://localhost:8080";
var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL") ?? "http://cv-user-service:8082";
var keycloakAdminUsername = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_USERNAME") ?? "admin";
var keycloakAdminPassword = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_PASSWORD") ?? "admin";

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

// ── In-memory session cache ──────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();

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

// Use PostConfigure to wire up the server-side session store
// (avoids "use before declare" error with 'app' variable)
builder.Services.AddSingleton<ITicketStore>(sp =>
    new MemoryCacheTicketStore(sp.GetRequiredService<IDistributedCache>()));
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore>((options, store) => options.SessionStore = store);

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

// ── Kafka Producer ──────────────────────────────────────────────────────────
var kafkaBootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092";
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaBootstrapServers,
        Acks = Acks.Leader,
        MessageTimeoutMs = 5000,
        RequestTimeoutMs = 5000,
        RetryBackoffMs = 100,
    };
    return new ProducerBuilder<string, string>(config).Build();
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

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
        + $"&prompt=login"
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
    // Retrieve id_token from server-side session before signing out
    var authResult = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    var idToken = authResult?.Properties?.Items?.TryGetValue("id_token", out var t) == true ? t ?? "" : "";

    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    var logoutUrl = $"{keycloakLogoutUrl}"
        + $"?client_id={Uri.EscapeDataString(keycloakClientId)}"
        + $"&id_token_hint={Uri.EscapeDataString(idToken)}"
        + $"&post_logout_redirect_uri={Uri.EscapeDataString("http://localhost:4200/login")}";

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

    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(idToken);
    var claims = jwt.Claims.ToList();

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    // Store only the essential access_token for proxy forwarding.
    // Avoid storing id_token/refresh_token — they make the cookie too large (>4 KB).
    identity.AddClaim(new Claim("access_token", accessToken));

    // Sync user to user-service and capture internal userId
    var userId = await SyncUserAsync(httpClientFactory, userServiceUrl, accessToken);
    if (!string.IsNullOrEmpty(userId))
    {
        identity.AddClaim(new Claim("user_id", userId));
    }

    var principal = new ClaimsPrincipal(identity);

    var authProps = new AuthenticationProperties
    {
        IsPersistent = true,
        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        RedirectUri = expectedReturn,
    };
    // Store id_token in server-side session props (not in cookie) for logout
    authProps.Items["id_token"] = idToken;

    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);

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
    var userId = result.Principal.FindFirstValue("user_id")
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
            },
        },
    });
})
.RequireCors("Default");

app.MapPost("/api/auth/register", async (HttpContext ctx, IHttpClientFactory httpClientFactory, IProducer<string, string> kafkaProducer) =>
{
    try
    {
        var body = await ctx.Request.ReadFromJsonAsync<JsonElement>();
        var email = body.GetProperty("email").GetString() ?? "";
        var password = body.GetProperty("password").GetString() ?? "";
        var firstName = body.GetProperty("firstName").GetString() ?? "";
        var lastName = body.GetProperty("lastName").GetString() ?? "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Email and password are required" });
            return;
        }

        var adminToken = await GetKeycloakAdminTokenAsync(httpClientFactory);
        if (adminToken == null)
        {
            ctx.Response.StatusCode = 502;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Failed to authenticate with Keycloak admin" });
            return;
        }

        var userCreated = await CreateKeycloakUserAsync(httpClientFactory, adminToken, email, password, firstName, lastName);
        if (!userCreated)
        {
            ctx.Response.StatusCode = 409;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "User with this email already exists" });
            return;
        }

        // Emit registration event to Kafka (fire-and-forget)
        _ = PublishRegistrationEvent(kafkaProducer, email, firstName, lastName);

        await ctx.Response.WriteAsJsonAsync(new { success = true, message = "Account created successfully. You can now login." });
    }
    catch (JsonException)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Invalid request body" });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Registration error: {ex.Message}");
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Registration failed due to an internal error" });
    }
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

            // ON COLLE LE POST-IT !
            var internalUserId = authResult.Principal?.FindFirstValue("user_id") 
                               ?? authResult.Principal?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                               ?? authResult.Principal?.FindFirstValue("sub");

            if (!string.IsNullOrEmpty(internalUserId))
            {
                ctx.Request.Headers["X-User-Id"] = internalUserId;
            }
        }
        await next();
    });
});

async Task<string?> SyncUserAsync(IHttpClientFactory factory, string baseUrl, string token)
{
    try
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsync($"{baseUrl}/api/users/sync", null);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"User sync failed ({response.StatusCode}): {body}");
            return null;
        }

        var bodyJson = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (bodyJson.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
        {
            return id.GetString();
        }

        return null;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"User sync error: {ex.Message}");
        return null;
    }
}

async Task<string?> GetKeycloakAdminTokenAsync(IHttpClientFactory factory)
{
    try
    {
        using var client = factory.CreateClient();
        var tokenContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", "admin-cli"),
            new KeyValuePair<string, string>("username", keycloakAdminUsername),
            new KeyValuePair<string, string>("password", keycloakAdminPassword),
            new KeyValuePair<string, string>("grant_type", "password"),
        });
        var tokenResponse = await client.PostAsync($"{keycloakInternalUrl}/realms/master/protocol/openid-connect/token", tokenContent);
        if (!tokenResponse.IsSuccessStatusCode)
            return null;
        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return tokenJson?.GetValueOrDefault("access_token")?.ToString();
    }
    catch
    {
        return null;
    }
}

async Task<bool> CreateKeycloakUserAsync(IHttpClientFactory factory, string adminToken, string email, string password, string firstName, string lastName)
{
    try
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var userData = new
        {
            username = email,
            email,
            firstName,
            lastName,
            enabled = true,
            emailVerified = true,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = password,
                    temporary = false,
                }
            },
        };

        var response = await client.PostAsJsonAsync($"{keycloakInternalUrl}/admin/realms/{keycloakRealm}/users", userData);
        if (!response.IsSuccessStatusCode) return false;

        var location = response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(location)) return true;

        var userId = location.Split('/').Last();

        var availableRolesResponse = await client.GetAsync($"{keycloakInternalUrl}/admin/realms/{keycloakRealm}/roles");
        if (!availableRolesResponse.IsSuccessStatusCode) return true;
        var roles = await availableRolesResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        if (roles == null) return true;

        var userRole = roles.FirstOrDefault(r =>
            r.GetProperty("name").GetString() == "user");

        if (userRole.ValueKind == JsonValueKind.Undefined) return true;

        var roleMapping = new[] { new
        {
            id = userRole.GetProperty("id").GetString(),
            name = userRole.GetProperty("name").GetString(),
        }};

        await client.PostAsJsonAsync(
            $"{keycloakInternalUrl}/admin/realms/{keycloakRealm}/users/{userId}/role-mappings/realm",
            roleMapping);

        return true;
    }
    catch
    {
        return false;
    }
}

async Task PublishRegistrationEvent(IProducer<string, string> producer, string email, string firstName, string lastName)
{
    try
    {
        var evt = new
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
        };
        var value = System.Text.Json.JsonSerializer.Serialize(evt);
        await producer.ProduceAsync("user.registered", new Message<string, string>
        {
            Key = email,
            Value = value,
        });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to publish registration event: {ex.Message}");

    }
}

app.Run();

class MemoryCacheTicketStore : ITicketStore
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public MemoryCacheTicketStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = "cv-session-" + Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _defaultExpiry,
            SlidingExpiration = TimeSpan.FromMinutes(30),
        };

        var bytes = SerializeTicket(ticket);
        await _cache.SetAsync(key, bytes, options);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var bytes = await _cache.GetAsync(key);
        if (bytes == null) return null;
        return DeserializeTicket(bytes);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    private static byte[] SerializeTicket(AuthenticationTicket ticket)
    {
        var claims = ticket.Principal.Claims.Select(c => new { c.Type, c.Value }).ToList();
        var data = new
        {
            Scheme = ticket.AuthenticationScheme,
            Claims = claims,
            AuthProps = ticket.Properties?.Items,
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, _jsonOptions));
    }

    private static AuthenticationTicket? DeserializeTicket(byte[] bytes)
    {
        try
        {
            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var scheme = root.GetProperty("Scheme").GetString() ?? "";
            var claimsList = root.GetProperty("Claims").EnumerateArray();
            var identity = new ClaimsIdentity(claimsList.Select(c =>
                new Claim(c.GetProperty("Type").GetString() ?? "",
                          c.GetProperty("Value").GetString() ?? "")), scheme);
            var principal = new ClaimsPrincipal(identity);
            var props = new AuthenticationProperties();

            if (root.TryGetProperty("AuthProps", out var authProps) && authProps.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in authProps.EnumerateObject())
                {
                    props.Items[prop.Name] = prop.Value.GetString();
                }
            }

            return new AuthenticationTicket(principal, props, scheme);
        }
        catch
        {
            return null;
        }
    }
}
