using Microsoft.EntityFrameworkCore;
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
