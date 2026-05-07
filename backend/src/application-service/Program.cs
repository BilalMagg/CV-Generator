using Microsoft.EntityFrameworkCore;
using FluentValidation;
using ApplicationService;
using ApplicationService.Services;
using ApplicationService.Repositories;
using ApplicationService.DTOs;
using ApplicationService.Validators;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// Services
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IApplicationStatusHistoryRepository, ApplicationStatusHistoryRepository>();
builder.Services.AddScoped<IKafkaPublisher, KafkaPublisher>();
builder.Services.AddScoped<ApplicationService.Services.IApplicationService, ApplicationServiceImpl>();

// Validators
builder.Services.AddScoped<IValidator<CreateApplicationDto>, CreateApplicationValidator>();
builder.Services.AddScoped<IValidator<UpdateStatusDto>, UpdateStatusValidator>();
builder.Services.AddScoped<IValidator<UpdateApplicationDto>, UpdateApplicationValidator>();

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

// ------------------------
// Run database migrations (only if needed)
// ------------------------
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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