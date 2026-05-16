using Confluent.Kafka;
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

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

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
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    EnableMultipleHttp2Connections = true,
    ConnectTimeout = TimeSpan.FromSeconds(5),
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
        var jwtAuthority = builder.Configuration["JWT_AUTHORITY"] ?? "";
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

// ── Kafka test endpoint ─────────────────────────────────────────────────────
app.MapGet("/api/test/kafka", async (HttpContext ctx, IConfiguration config) =>
{
    var results = new List<string>();
    var kafkaConfig = new ProducerConfig
    {
        BootstrapServers = config.GetValue<string>("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092",
        MessageTimeoutMs = 5000,
        RequestTimeoutMs = 5000,
    };
    results.Add($"Using KAFKA_BOOTSTRAP_SERVERS = '{kafkaConfig.BootstrapServers}'");

    try
    {
        using var producer = new ProducerBuilder<string, string>(kafkaConfig).Build();
        results.Add("Producer built successfully");

        var msg = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = "{\"test\":true,\"timestamp\":\"" + DateTime.UtcNow.ToString("O") + "\"}"
        };

        var dr = await producer.ProduceAsync("test-topic", msg);
        results.Add($"Published to {dr.TopicPartitionOffset}");
        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        results.Add($"FAILED: {ex.GetType().Name}: {ex.Message}");
        return Results.Ok(results);
    }
})
.WithName("TestKafka");

// ── gRPC test endpoint ──────────────────────────────────────────────────────
app.MapGet("/api/test/grpc", async (string? userId, IUserGrpcClientService userGrpc) =>
{
    var results = new List<string>();

    if (string.IsNullOrEmpty(userId))
    {
        results.Add("Provide ?userId={guid} query parameter");
        return Results.Ok(results);
    }

    if (!Guid.TryParse(userId, out var parsed))
    {
        results.Add($"Invalid userId format: '{userId}'");
        return Results.Ok(results);
    }

    results.Add($"Looking up userId={parsed} via gRPC...");

    try
    {
        var exists = await userGrpc.UserExistsAsync(parsed);
        results.Add($"UserExistsAsync => {exists}");

        var user = await userGrpc.GetUserAsync(parsed);
        if (user != null)
            results.Add($"GetUserAsync => Id={user.Id}, Email={user.Email}, Name={user.FirstName} {user.LastName}");
        else
            results.Add("GetUserAsync => null (not found)");

        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        results.Add($"FAILED: {ex.GetType().Name}: {ex.Message}");
        return Results.Ok(results);
    }
})
.WithName("TestGrpc");

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();