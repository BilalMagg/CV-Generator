using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using FluentValidation;
using CommonProtos.User;
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
builder.Services.AddScoped<IUserGrpcClientService, UserGrpcClientService>();
builder.Services.AddScoped<ApplicationService.Services.IApplicationService, ApplicationServiceImpl>();

// gRPC client — UserService
builder.Services.AddGrpcClient<UserServiceGrpc.UserServiceGrpcClient>(o =>
{
    var grpcUrl = builder.Configuration.GetValue<string>("USER_SERVICE_GRPC_URL")
        ?? Environment.GetEnvironmentVariable("USER_SERVICE_GRPC_URL")
        ?? "http://cv-user-service:8082";
    o.Address = new Uri(grpcUrl);
})
.ConfigureChannel(o =>
{
    o.HttpHandler = new SocketsHttpHandler
    {
        EnableMultipleHttp2Connections = true,
        ConnectTimeout = TimeSpan.FromSeconds(5),
    };
});

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
        var jwtAuthority = Environment.GetEnvironmentVariable("JWT_AUTHORITY") ?? "";
        options.Authority = jwtAuthority;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters.ValidateAudience = false;
        // Accept the external URL as a valid issuer (the JWT's iss claim)
        options.TokenValidationParameters.ValidIssuers = new[]
        {
            "http://localhost:9090/realms/cv-realm",
        };
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