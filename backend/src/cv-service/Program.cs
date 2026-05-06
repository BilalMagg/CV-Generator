using Microsoft.EntityFrameworkCore;
using FluentValidation;
using CvService;
using CvService.DTOs;
using CvService.Services;
using CvService.Validators;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<CvDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// Repositories
// TODO: Register repository implementations
// builder.Services.AddScoped<ICvRepository, CvRepository>();
// builder.Services.AddScoped<ICvVersionRepository, CvVersionRepository>();
// builder.Services.AddScoped<ICvSectionRepository, CvSectionRepository>();

// Services
builder.Services.AddScoped<ICvService, CvServiceImpl>();
builder.Services.AddScoped<ICvVersionService, CvVersionServiceImpl>();
builder.Services.AddScoped<ICvSectionService, CvSectionServiceImpl>();

// Validators
builder.Services.AddScoped<IValidator<CreateCvDto>, CreateCvValidator>();

// AutoMapper
builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Auth (JWT from gateway/keycloak)
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = Environment.GetEnvironmentVariable("JWT_AUTHORITY") ?? "";
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Run database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CvDbContext>();
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

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
