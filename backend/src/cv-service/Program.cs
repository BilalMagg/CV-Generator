using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using CvService;
using CvService.DTOs;
using CvService.Grpc;
using CvService.Repositories;
using CvService.Services;
using CvService.Validators;
using CommonProtos.CV;

var builder = WebApplication.CreateBuilder(args);

var httpPort = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "8088");
var grpcPort = int.Parse(Environment.GetEnvironmentVariable("GRPC_PORT") ?? "18088");

builder.WebHost.ConfigureKestrel(options =>
  {
      options.ListenAnyIP(httpPort, listenOptions =>
      {
          listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
      });
      options.ListenAnyIP(grpcPort, listenOption =>
      {
          listenOption.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
      });
  });

// Database
builder.Services.AddDbContext<CvDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// Repositories
builder.Services.AddScoped<ICvRepository, CvRepository>();
builder.Services.AddScoped<ICvVersionRepository, CvVersionRepository>();
builder.Services.AddScoped<ICvSectionRepository, CvSectionRepository>();

// Services
builder.Services.AddScoped<ICvService, CvServiceImpl>();
builder.Services.AddScoped<ICvVersionService, CvVersionServiceImpl>();
builder.Services.AddScoped<ICvSectionService, CvSectionServiceImpl>();

builder.Services.AddScoped<IKafkaPublisher, KafkaPublisher>();

// Validators
builder.Services.AddScoped<IValidator<CreateCvDto>, CreateCvValidator>();

// AutoMapper
builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//gRPC
builder.Services.AddGrpc();

// Auth (JWT from gateway/keycloak)
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var jwtAuthority = Environment.GetEnvironmentVariable("JWT_AUTHORITY")
            ?? builder.Configuration["JWT_AUTHORITY"]
            ?? "http://localhost:9090/realms/cv-realm";
        var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
            ?? $"http://{Environment.GetEnvironmentVariable("KEYCLOAK_EXTERNAL_HOST") ?? "localhost"}:{Environment.GetEnvironmentVariable("KEYCLOAK_EXTERNAL_PORT") ?? "9090"}/realms/cv-realm";
        options.Authority = jwtAuthority;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.TokenValidationParameters.ValidIssuers = new[]
        {
            jwtIssuer,
        };
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

// ── Kafka test endpoint ─────────────────────────────────────────────────────
app.MapGet("/api/test/kafka", async (IConfiguration config) =>
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

        var dr = await producer.ProduceAsync("cv-test-topic", msg);
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

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<CvServiceImp>();

app.Run();
