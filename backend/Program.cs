using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
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

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = KeycloakConfig.Authority;
    options.MetadataAddress = KeycloakConfig.Authority.Replace("keycloak", "host.docker.internal");
    options.ClientId = KeycloakConfig.ClientId;
    options.ClientSecret = KeycloakConfig.ClientSecret;
    options.ResponseType = KeycloakConfig.ResponseType;
    options.CallbackPath = KeycloakConfig.CallbackPath;
    options.RequireHttpsMetadata = false;
    options.SaveTokens = true;

    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");

    options.TokenValidationParameters.NameClaimType = "preferred_username";
    options.TokenValidationParameters.RoleClaimType = "realm_access.roles";
});

// If you want JWT auth later, you can configure it here
// builder.Services.AddAuthentication(...);

// Register your services / repositories here
// builder.Services.AddScoped<IUserService, UserService>();
// builder.Services.AddScoped<IAuthService, AuthService>();

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
        // Check if there are pending migrations
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Found {Count} pending migrations. Applying...", pendingMigrations.Count());
            
            // Apply pending migrations
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
        // Decide: either throw (app won't start) or continue with warning
        // throw; // Uncomment to prevent app start on migration failure
    }
}

// ------------------------
// 2. Configure middleware
// ------------------------

// Enable Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable HTTPS
// app.UseHttpsRedirection();

// Enable authentication & authorization if used
// app.UseAuthentication();
// app.UseAuthorization();
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Hello from backend!");
    app.MapGet("/secure", [Authorize] () => "Hello from secure backend!");
// Map controllers
app.MapControllers();

// ------------------------
// 3. Run the app
// ------------------------
app.Run();
