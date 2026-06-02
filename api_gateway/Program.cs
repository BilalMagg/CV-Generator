using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Confluent.Kafka;


var builder = WebApplication.CreateBuilder(args);

// ── Environment variables with fallbacks ─────────────────────────────────────
string Env(string key, string fallback) =>
    Environment.GetEnvironmentVariable(key) ?? fallback;

var keycloakHost = Env("KEYCLOAK_HOST", "keycloak");
var keycloakPort = Env("KEYCLOAK_PORT", "8080");
var keycloakExtHost = Env("KEYCLOAK_EXTERNAL_HOST", "localhost");
var keycloakExtPort = Env("KEYCLOAK_EXTERNAL_PORT", "9090");

var keycloakInternalUrl = $"http://{keycloakHost}:{keycloakPort}";
var keycloakExternalUrl = $"http://{keycloakExtHost}:{keycloakExtPort}";

var keycloakRealm = Env("KEYCLOAK_REALM", "cv-realm");
var keycloakClientId = Env("KEYCLOAK_CLIENT_ID", "cv-gateway");
var keycloakClientSecret = Env("KEYCLOAK_CLIENT_SECRET", "change-me-in-production");

var gatewayHost = Env("GATEWAY_HOST", "localhost");
var gatewayPort = Env("GATEWAY_PORT", "8080");
var gatewayUrl = $"http://{gatewayHost}:{gatewayPort}";

builder.WebHost.UseUrls($"http://0.0.0.0:{gatewayPort}");

var frontendHost = Env("FRONTEND_HOST", "localhost");
var frontendPort = Env("FRONTEND_PORT", "4200");
var frontendUrl = $"http://{frontendHost}:{frontendPort}";

var userServiceUrl = $"http://{Env("USER_SERVICE_HOST", "cv-user-service")}:{Env("USER_SERVICE_PORT", "8082")}";

var keycloakAdminUsername = Env("KEYCLOAK_ADMIN_USERNAME", "admin");
var keycloakAdminPassword = Env("KEYCLOAK_ADMIN_PASSWORD", "admin");

var kafkaBootstrapServers = $"{Env("KAFKA_HOST", "kafka")}:{Env("KAFKA_PORT", "9092")}";

var authority = $"{keycloakInternalUrl}/realms/{keycloakRealm}";
var keycloakLoginUrl = $"{keycloakExternalUrl}/realms/{keycloakRealm}/protocol/openid-connect/auth";
var keycloakLogoutUrl = $"{keycloakExternalUrl}/realms/{keycloakRealm}/protocol/openid-connect/logout";

// ── Override YARP cluster addresses from env vars ──────────────────────────
// (config file has fallback addresses; env vars take precedence at runtime)
void SetClusterAddress(string clusterId, string hostVar, string portVar, string defaultHost, string defaultPort)
{
    var host = Environment.GetEnvironmentVariable(hostVar) ?? defaultHost;
    var port = Environment.GetEnvironmentVariable(portVar) ?? defaultPort;
    builder.Configuration[$"Proxy:Clusters:{clusterId}:Destinations:destination-1:Address"] = $"http://{host}:{port}";
}

SetClusterAddress("user-cluster",          "USER_SERVICE_HOST",          "USER_SERVICE_PORT",          "cv-user-service",          "8082");
SetClusterAddress("content-cluster",       "CONTENT_SERVICE_HOST",       "CONTENT_SERVICE_PORT",       "cv-user-content-service",  "8083");
SetClusterAddress("workflow-cluster",      "WORKFLOW_SERVICE_HOST",      "WORKFLOW_SERVICE_PORT",      "cv-workflow-service",      "8084");
SetClusterAddress("application-cluster",   "APPLICATION_SERVICE_HOST",   "APPLICATION_SERVICE_PORT",   "cv-application-service",   "8085");
SetClusterAddress("job-offer-cluster",     "JOB_OFFER_SERVICE_HOST",     "JOB_OFFER_SERVICE_PORT",     "cv-job-offer-service",     "8086");
SetClusterAddress("notification-cluster",  "NOTIFICATION_SERVICE_HOST",  "NOTIFICATION_SERVICE_PORT",  "cv-notification-service",  "8087");
SetClusterAddress("cv-cluster",            "CV_SERVICE_HOST",            "CV_SERVICE_PORT",            "cv-cv-service",            "8088");

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(frontendUrl)
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("default", policy => policy.RequireAuthenticatedUser());
});

// ── Reverse Proxy (routes from JSON, clusters from env vars) ─────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("Proxy"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── HTTP Client for token exchange ───────────────────────────────────────────
builder.Services.AddHttpClient();

// ── Kafka Producer ───────────────────────────────────────────────────────────
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

// ── Health Check ─────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "api-gateway" }));

// ── Auth Endpoints ───────────────────────────────────────────────────────────
app.MapGet("/api/auth/login", async (HttpContext ctx, string? returnUrl, string? provider) =>
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

    if (!string.IsNullOrEmpty(provider))
    {
        loginUrl += $"&kc_idp_hint={Uri.EscapeDataString(provider)}";
    }

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

app.MapPost("/api/auth/login", async (HttpContext ctx, IHttpClientFactory httpClientFactory) =>
{
    try
    {
        var body = await ctx.Request.ReadFromJsonAsync<JsonElement>();
        var email = body.GetProperty("email").GetString() ?? "";
        var password = body.GetProperty("password").GetString() ?? "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Email and password are required" });
            return;
        }

        var tokenUrl = $"{keycloakInternalUrl}/realms/{keycloakRealm}/protocol/openid-connect/token";
        var http = httpClientFactory.CreateClient();
        var tokenContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", keycloakClientId),
            new KeyValuePair<string, string>("client_secret", keycloakClientSecret),
            new KeyValuePair<string, string>("username", email),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("scope", "openid profile email"),
        });

        var tokenResponse = await http.PostAsync(tokenUrl, tokenContent);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Invalid email or password" });
            return;
        }

        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        if (tokenJson == null)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Invalid token response" });
            return;
        }

        var accessToken = tokenJson.GetValueOrDefault("access_token")?.ToString() ?? "";
        var idToken = tokenJson.GetValueOrDefault("id_token")?.ToString() ?? "";

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(string.IsNullOrEmpty(idToken) ? accessToken : idToken);
        var claims = jwt.Claims.ToList();

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim("access_token", accessToken));

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
        };
        authProps.Items["id_token"] = idToken;

        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);

        var firstName = jwt.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value ?? "";
        var lastName = jwt.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value ?? "";
        var jwtEmail = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "";
        var sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "";

        await ctx.Response.WriteAsJsonAsync(new
        {
            success = true,
            data = new
            {
                userId = userId ?? sub,
                keycloakId = sub,
                firstName,
                lastName,
                email = jwtEmail,
                role = "user",
                isActive = true,
                tokens = new { accessToken },
            },
        });
    }
    catch (JsonException)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Invalid request body" });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Login error: {ex.Message}");
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Login failed due to an internal error" });
    }
})
.RequireCors("Default");

app.MapGet("/api/auth/logout", async (HttpContext ctx, IHttpClientFactory httpClientFactory) =>
{
    var postLogoutRedirectUri = $"{frontendUrl}/login";

    var authResult = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    var idToken = authResult?.Properties?.Items?.TryGetValue("id_token", out var t) == true ? t ?? "" : "";

    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    if (!string.IsNullOrEmpty(idToken))
    {
        var client = httpClientFactory.CreateClient();
        var keycloakLogoutUrl = $"{keycloakInternalUrl}/realms/{keycloakRealm}/protocol/openid-connect/logout"
            + $"?id_token_hint={Uri.EscapeDataString(idToken)}"
            + $"&client_id={Uri.EscapeDataString(keycloakClientId)}";
        _ = client.GetAsync(keycloakLogoutUrl);
    }

    ctx.Response.Redirect(postLogoutRedirectUri);
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
    identity.AddClaim(new Claim("access_token", accessToken));

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

        var keycloakUserId = await CreateKeycloakUserAsync(httpClientFactory, adminToken, email, password, firstName, lastName);
        if (string.IsNullOrEmpty(keycloakUserId))
        {
            ctx.Response.StatusCode = 409;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "User with this email already exists" });
            return;
        }

        var internalUserId = await CreateUserInServiceAsync(httpClientFactory, userServiceUrl, keycloakUserId, email, firstName, lastName);

        _ = PublishRegistrationEvent(kafkaProducer, internalUserId ?? "", email, firstName, lastName);

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

async Task<string?> CreateKeycloakUserAsync(IHttpClientFactory factory, string adminToken, string email, string password, string firstName, string lastName)
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
        if (!response.IsSuccessStatusCode) return null;

        var location = response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(location)) return null;

        var userId = location.Split('/').Last();

        var availableRolesResponse = await client.GetAsync($"{keycloakInternalUrl}/admin/realms/{keycloakRealm}/roles");
        if (!availableRolesResponse.IsSuccessStatusCode) return userId;
        var roles = await availableRolesResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        if (roles == null) return userId;

        var userRole = roles.FirstOrDefault(r =>
            r.GetProperty("name").GetString() == "user");

        if (userRole.ValueKind == JsonValueKind.Undefined) return userId;

        var roleMapping = new[] { new
        {
            id = userRole.GetProperty("id").GetString(),
            name = userRole.GetProperty("name").GetString(),
        }};

        await client.PostAsJsonAsync(
            $"{keycloakInternalUrl}/admin/realms/{keycloakRealm}/users/{userId}/role-mappings/realm",
            roleMapping);

        return userId;
    }
    catch
    {
        return null;
    }
}

async Task PublishRegistrationEvent(IProducer<string, string> producer, string internalUserId, string email, string firstName, string lastName)
{
    try
    {
        var evt = new
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            InternalUserId = string.IsNullOrEmpty(internalUserId) ? null : internalUserId,
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

async Task<string?> CreateUserInServiceAsync(IHttpClientFactory factory, string baseUrl, string keycloakId, string email, string firstName, string lastName)
{
    try
    {
        using var client = factory.CreateClient();
        var payload = new
        {
            keycloakId,
            firstName,
            lastName,
            email,
            role = "USER",
        };
        var response = await client.PostAsJsonAsync($"{baseUrl}/api/users", payload);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"User creation in user-service failed ({response.StatusCode}): {body}");
            return null;
        }
        var bodyJson = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (bodyJson.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
            return id.GetString();
        return null;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"User creation error: {ex.Message}");
        return null;
    }
}

// ── Configure Keycloak realm (identity providers + client redirect URIs) ────
_ = ConfigureKeycloakAsync();

app.Run();

static async Task ConfigureKeycloakAsync()
{
    var keycloakHost = Environment.GetEnvironmentVariable("KEYCLOAK_HOST") ?? "keycloak";
    var keycloakPort = Environment.GetEnvironmentVariable("KEYCLOAK_PORT") ?? "8080";
    var keycloakExtHost = Environment.GetEnvironmentVariable("KEYCLOAK_EXTERNAL_HOST") ?? "localhost";
    var keycloakExtPort = Environment.GetEnvironmentVariable("KEYCLOAK_EXTERNAL_PORT") ?? "9090";

    var keycloakInternalUrl = $"http://{keycloakHost}:{keycloakPort}";
    var keycloakExternalUrl = $"http://{keycloakExtHost}:{keycloakExtPort}";

    var keycloakRealm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM") ?? "cv-realm";
    var keycloakAdminUsername = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_USERNAME") ?? "admin";
    var keycloakAdminPassword = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_PASSWORD") ?? "admin";

    var gatewayHost = Environment.GetEnvironmentVariable("GATEWAY_HOST") ?? "localhost";
    var gatewayPort = Environment.GetEnvironmentVariable("GATEWAY_PORT") ?? "8080";
    var gatewayUrl = $"http://{gatewayHost}:{gatewayPort}";

    var frontendHost = Environment.GetEnvironmentVariable("FRONTEND_HOST") ?? "localhost";
    var frontendPort = Environment.GetEnvironmentVariable("FRONTEND_PORT") ?? "4200";
    var frontendUrl = $"http://{frontendHost}:{frontendPort}";

    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    const int maxRetries = 30;
    const int delayMs = 2000;

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var tokenContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", "admin-cli"),
                new KeyValuePair<string, string>("username", keycloakAdminUsername),
                new KeyValuePair<string, string>("password", keycloakAdminPassword),
                new KeyValuePair<string, string>("grant_type", "password"),
            });

            var tokenResponse = await client.PostAsync($"{keycloakInternalUrl}/realms/master/protocol/openid-connect/token", tokenContent);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                if (attempt < maxRetries) { await Task.Delay(delayMs); continue; }
                Console.Error.WriteLine("Failed to get Keycloak admin token after retries");
                return;
            }

            var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            var adminToken = tokenJson?.GetValueOrDefault("access_token")?.ToString();
            if (string.IsNullOrEmpty(adminToken))
            {
                if (attempt < maxRetries) { await Task.Delay(delayMs); continue; }
                Console.Error.WriteLine("Empty Keycloak admin token");
                return;
            }

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            // ── Configure redirect URIs for cv-gateway client ────────────────
            var gatewayRedirectUris = new[]
            {
                $"{gatewayUrl}/api/auth/callback",
                $"{gatewayUrl}/*",
            };
            var gatewayWebOrigins = new[] { gatewayUrl };

            // ── Configure redirect URIs for frontend client ──────────────────
            var frontendRedirectUris = new[] { $"{frontendUrl}/*" };
            var frontendWebOrigins = new[] { frontendUrl };

            await ConfigureClientUrisAsync(client, keycloakInternalUrl, keycloakRealm, "cv-gateway", gatewayRedirectUris, gatewayWebOrigins);
            await ConfigureClientUrisAsync(client, keycloakInternalUrl, keycloakRealm, "cv-frontend", frontendRedirectUris, frontendWebOrigins);

            // ── Configure identity providers (Google, GitHub) ────────────────
            var providers = new[]
            {
                new { Alias = "google", ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"), ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") },
                new { Alias = "github", ClientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID"), ClientSecret = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET") },
            };

            var allSucceeded = true;

            foreach (var provider in providers)
            {
                if (string.IsNullOrEmpty(provider.ClientId) || string.IsNullOrEmpty(provider.ClientSecret))
                {
                    Console.WriteLine($"Skipping {provider.Alias} identity provider — env vars not set");
                    continue;
                }

                var alias = provider.Alias;

                var createConfig = new Dictionary<string, string>
                {
                    ["clientId"] = provider.ClientId,
                    ["clientSecret"] = provider.ClientSecret,
                };
                if (alias == "google")
                    createConfig["defaultScope"] = "openid profile email";

                var createPayload = new Dictionary<string, object>
                {
                    ["alias"] = alias,
                    ["providerId"] = alias,
                    ["enabled"] = true,
                    ["config"] = createConfig,
                };

                var createResponse = await client.PostAsJsonAsync(
                    $"{keycloakInternalUrl}/admin/realms/{keycloakRealm}/identity-provider/instances",
                    createPayload);

                if (createResponse.IsSuccessStatusCode || createResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    var getResponse = await client.GetAsync(
                        $"{keycloakInternalUrl}/admin/realms/{keycloakRealm}/identity-provider/instances/{alias}");

                    if (!getResponse.IsSuccessStatusCode)
                    {
                        allSucceeded = false;
                        Console.Error.WriteLine($"Failed to get {alias} identity provider: {getResponse.StatusCode}");
                        continue;
                    }

                    var existing = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
                    var config = existing.GetProperty("config").EnumerateObject()
                        .ToDictionary(kv => kv.Name, kv => kv.Value.GetString() ?? "");

                    config["clientId"] = provider.ClientId;
                    config["clientSecret"] = provider.ClientSecret;
                    if (alias == "google")
                        config["defaultScope"] = "openid profile email";

                    var patch = new Dictionary<string, object>
                    {
                        ["alias"] = alias,
                        ["providerId"] = existing.GetProperty("providerId").GetString() ?? alias,
                        ["enabled"] = true,
                        ["config"] = config,
                    };

                    var putResponse = await client.PutAsJsonAsync(
                        $"{keycloakInternalUrl}/admin/realms/{keycloakRealm}/identity-provider/instances/{alias}",
                        patch);

                    if (putResponse.IsSuccessStatusCode)
                        Console.WriteLine($"Configured {alias} identity provider");
                    else
                    {
                        allSucceeded = false;
                        var err = await putResponse.Content.ReadAsStringAsync();
                        Console.Error.WriteLine($"Failed to configure {alias}: {putResponse.StatusCode} {err}");
                    }
                }
                else
                {
                    allSucceeded = false;
                    var err = await createResponse.Content.ReadAsStringAsync();
                    Console.Error.WriteLine($"Failed to create {alias} identity provider: {createResponse.StatusCode} {err}");
                }
            }

            if (allSucceeded)
            {
                Console.WriteLine("Keycloak configuration completed successfully");
                return;
            }

            if (attempt < maxRetries)
            {
                Console.WriteLine($"Retrying Keycloak configuration ({attempt}/{maxRetries})...");
                await Task.Delay(delayMs);
            }
            else
                Console.Error.WriteLine("Failed to configure Keycloak after all retries");
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            Console.WriteLine($"Keycloak configuration attempt {attempt}/{maxRetries} failed: {ex.Message}");
            await Task.Delay(delayMs);
        }
    }
}

static async Task ConfigureClientUrisAsync(HttpClient client, string keycloakUrl, string realm, string clientId, string[] redirectUris, string[] webOrigins)
{
    try
    {
        var getResponse = await client.GetAsync($"{keycloakUrl}/admin/realms/{realm}/clients");
        if (!getResponse.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Failed to list clients: {getResponse.StatusCode}");
            return;
        }

        var clients = await getResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        if (clients == null) return;

        var targetClient = clients.FirstOrDefault(c =>
            c.GetProperty("clientId").GetString() == clientId);

        if (targetClient.ValueKind == JsonValueKind.Undefined)
        {
            Console.WriteLine($"Client '{clientId}' not found in Keycloak, skipping redirect URIs config");
            return;
        }

        var currentRedirectUris = targetClient.GetProperty("redirectUris").EnumerateArray()
            .Select(u => u.GetString()).Where(u => u != null).Cast<string>().ToList();
        var currentWebOrigins = targetClient.GetProperty("webOrigins").EnumerateArray()
            .Select(o => o.GetString()).Where(o => o != null).Cast<string>().ToList();

        var mergedRedirectUris = currentRedirectUris
            .Concat(redirectUris.Where(u => !currentRedirectUris.Contains(u)))
            .ToList();

        var mergedWebOrigins = currentWebOrigins
            .Concat(webOrigins.Where(o => !currentWebOrigins.Contains(o)))
            .ToList();

        if (mergedRedirectUris.Count == currentRedirectUris.Count && mergedWebOrigins.Count == currentWebOrigins.Count)
        {
            Console.WriteLine($"Client '{clientId}' redirect URIs are up to date");
            return;
        }

        var updatePayload = new Dictionary<string, object>
        {
            ["redirectUris"] = mergedRedirectUris,
            ["webOrigins"] = mergedWebOrigins,
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"{keycloakUrl}/admin/realms/{realm}/clients/{targetClient.GetProperty("id").GetString()}",
            updatePayload);

        if (updateResponse.IsSuccessStatusCode)
            Console.WriteLine($"Updated '{clientId}' redirect URIs: {string.Join(", ", mergedRedirectUris)}");
        else
        {
            var err = await updateResponse.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"Failed to update '{clientId}' client: {updateResponse.StatusCode} {err}");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error configuring client '{clientId}': {ex.Message}");
    }
}

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
