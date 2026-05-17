using Microsoft.EntityFrameworkCore;
using FluentValidation;
using JobOfferService;
using JobOfferService.Services;
using JobOfferService.Repositories;
using JobOfferService.DTOs;
using JobOfferService.Validators;
using JobOfferService.Hubs;
using JobOfferService.Workers;

var builder = WebApplication.CreateBuilder(args);

// 1. Database (Configured with pgvector via the DbContext)
builder.Services.AddDbContext<JobOfferDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, o => o.UseVector());
});

// 2. Repositories
builder.Services.AddScoped<IJobOfferRepository, JobOfferRepository>();
builder.Services.AddScoped<ISearchCacheRepository, SearchCacheRepository>();
builder.Services.AddScoped<IUserQuotaRepository, UserQuotaRepository>();

// 3. Services
builder.Services.AddScoped<IJobOfferService, JobOfferService.Services.JobOfferService>();
builder.Services.AddScoped<IKafkaPublisher, KafkaPublisher>();

// 4. Validators
builder.Services.AddScoped<IValidator<SubmitJobOfferDto>, SubmitJobOfferValidator>();
builder.Services.AddScoped<IValidator<ExtractedJobDto>, ExtractedJobValidator>();
builder.Services.AddScoped<IValidator<UpdateJobStatusDto>, UpdateJobStatusValidator>();

// 5. AutoMapper
builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());

// 6. Controllers & API Explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 7. SignalR
builder.Services.AddSignalR();

// 8. Kafka background workers
builder.Services.AddHostedService<CrawlSummaryConsumer>();

// 9. Auth (JWT from gateway/keycloak)
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = Environment.GetEnvironmentVariable("JWT_AUTHORITY") ?? "";
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

// 10. CORS — needed for SignalR WebSocket handshake from the frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// ------------------------
// Run database migrations on startup
// ------------------------
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<JobOfferDbContext>();
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
// Middleware Pipeline
// ------------------------
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Map the SignalR hub at /hubs/jobs
app.MapHub<JobHub>("/hubs/jobs");

app.Run();