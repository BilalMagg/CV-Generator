é=
4/app/src/user-service/Controllers/UsersController.cs¿<using System.Globalization;
using System.Security.Claims;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService;
using UserService.Entity;
using UserService.Events;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly IKafkaPublisher _kafka;
    private readonly ILogger<UsersController> _logger;

    public UsersController(UserDbContext db, IKafkaPublisher kafka, ILogger<UsersController> logger)
    {
        _db = db;
        _kafka = kafka;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users.ToListAsync();
        return Ok(ApiResponse<List<UserResponseDto>>.Ok(users.Select(ToDto).ToList()));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(ApiResponse<UserResponseDto>.Error("User not found"));
        return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var keycloakId = User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized(ApiResponse<UserResponseDto>.Error("Invalid token"));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null)
        {
            user = new User
            {
                KeycloakId = keycloakId,
                FirstName = User.FindFirstValue("given_name") ?? "",
                LastName = User.FindFirstValue("family_name") ?? "",
                Email = User.FindFirstValue("email") ?? "",
                Role = Role.USER,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created user {Id} from JWT (sub={KeycloakId})", user.Id, keycloakId);

            await _kafka.PublishAsync(new UserCreatedEvent
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
            }, "user.created");
        }

        return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
    }

    [HttpPost("sync")]
    [Authorize]
    public async Task<IActionResult> Sync()
    {
        var keycloakId = User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(keycloakId))
            return Unauthorized(ApiResponse<UserResponseDto>.Error("Invalid token"));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        var firstName = User.FindFirstValue("given_name") ?? "";
        var lastName = User.FindFirstValue("family_name") ?? "";
        var email = User.FindFirstValue("email") ?? "";

        if (user == null)
        {
            user = new User
            {
                KeycloakId = keycloakId,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Role = Role.USER,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.Users.Add(user);
            _logger.LogInformation("Created user {Id} via sync", user.Id);
        }
        else
        {
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;

            await _db.SaveChangesAsync();
            return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
        }

        await _db.SaveChangesAsync();

        await _kafka.PublishAsync(new UserCreatedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
        }, "user.created");

        return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var existing = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (existing)
            return Conflict(ApiResponse<UserResponseDto>.Error("Email already exists"));

        var user = new User
        {
            KeycloakId = dto.KeycloakId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Role = Enum.Parse<Role>(dto.Role),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created user {Id}", user.Id);

        await _kafka.PublishAsync(new UserCreatedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
        }, "user.created");

        return Created($"/api/users/{user.Id}", ApiResponse<UserResponseDto>.Created(ToDto(user)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(ApiResponse<UserResponseDto>.Error("User not found"));

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.PhoneNumber = dto.PhoneNumber;
        if (dto.BirthDate != null && DateTime.TryParse(dto.BirthDate, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var birthDate))
            user.BirthDate = DateTime.SpecifyKind(birthDate, DateTimeKind.Utc);
        user.AvatarUrl = dto.AvatarUrl;
        user.PreferencesJson = dto.PreferencesJson;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<UserResponseDto>.Ok(ToDto(user)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(ApiResponse<object>.Error("User not found"));

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateUserDto(
        string KeycloakId,
        string FirstName,
        string LastName,
        string Email,
        string Role
    );

    public record UpdateUserDto(
        string FirstName,
        string LastName,
        string? PhoneNumber,
        string? BirthDate,
        string? AvatarUrl,
        string? PreferencesJson
    );

    public record UserResponseDto(
        Guid Id,
        string KeycloakId,
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string? BirthDate,
        string Role,
        string? AvatarUrl,
        DateTime CreatedAt,
        DateTime? LastLogin,
        bool IsActive,
        string? AiProfileDataJson,
        string? PreferencesJson
    );

    private static UserResponseDto ToDto(User u) => new(
        u.Id, u.KeycloakId, u.FirstName, u.LastName, u.Email,
        u.PhoneNumber, u.BirthDate?.ToString("O"), u.Role.ToString(),
        u.AvatarUrl, u.CreatedAt, u.LastLogin, u.IsActive,
        u.AiProfileDataJson, u.PreferencesJson
    );
}
ParseOptions.0.jsonÈ

$/app/src/user-service/Entity/User.cs´
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UserService.Entity;

[Table("users")]
[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string KeycloakId { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public required string FirstName { get; set; }

    [Required]
    [MaxLength(50)]
    public required string LastName { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public required string Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public DateTime? BirthDate { get; set; }

    [Required]
    [MaxLength(20)]
    public Role Role { get; set; } = Role.USER;

    [MaxLength(255)]
    public string? AvatarUrl { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLogin { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    public string? AiProfileDataJson { get; set; }
    public string? PreferencesJson { get; set; }
}

public enum Role
{
    USER,
    ADMIN,
}ParseOptions.0.jsonı
*/app/src/user-service/Events/UserEvents.cs±namespace UserService.Events;

public abstract record UserEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record UserCreatedEvent : UserEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
}
ParseOptions.0.json”!
-/app/src/user-service/Grpc/UserServiceImpl.cså!using CommonProtos.User;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using UserService;
using UserService.Entity;

namespace UserService.Grpc;

public class UserServiceImpl : CommonProtos.User.UserServiceGrpc.UserServiceGrpcBase
{
    private readonly UserDbContext _db;
    private readonly ILogger<UserServiceImpl> _logger;

    public UserServiceImpl(UserDbContext db, ILogger<UserServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<UserProto> GetUserById(GetUserByIdRequest request, ServerCallContext context)
    {
        var user = await _db.Users.FindAsync(Guid.Parse(request.Id));
        if (user == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        return ToProto(user);
    }

    public override async Task<UserProto> GetUserByEmail(GetUserByEmailRequest request, ServerCallContext context)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        return ToProto(user);
    }

    public override async Task<UserProto> GetUserByKeycloakId(GetUserByKeycloakIdRequest request, ServerCallContext context)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == request.KeycloakId);
        if (user == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        return ToProto(user);
    }

    public override async Task<UserExistsResponse> UserExists(UserExistsRequest request, ServerCallContext context)
    {
        var exists = await _db.Users.AnyAsync(u => u.Id == Guid.Parse(request.Id));
        return new UserExistsResponse { Exists = exists };
    }

    public override async Task<UserProto> CreateUser(CreateUserRequest request, ServerCallContext context)
    {
        var user = new User
        {
            KeycloakId = request.KeycloakId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Role = Enum.Parse<Role>(request.Role),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created user {Id} via gRPC", user.Id);
        return ToProto(user);
    }

    public override async Task<UserProto> UpdateUser(UpdateUserRequest request, ServerCallContext context)
    {
        var user = await _db.Users.FindAsync(Guid.Parse(request.Id));
        if (user == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.AvatarUrl = request.AvatarUrl;
        user.PreferencesJson = request.PreferencesJson;

        await _db.SaveChangesAsync();
        return ToProto(user);
    }

    public override async Task<DeleteUserResponse> DeleteUser(DeleteUserRequest request, ServerCallContext context)
    {
        var user = await _db.Users.FindAsync(Guid.Parse(request.Id));
        if (user == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return new DeleteUserResponse { Success = true };
    }

    private static UserProto ToProto(User u) => new()
    {
        Id = u.Id.ToString(),
        KeycloakId = u.KeycloakId,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        PhoneNumber = u.PhoneNumber ?? "",
        BirthDate = u.BirthDate?.ToString("O") ?? "",
        Role = u.Role.ToString(),
        AvatarUrl = u.AvatarUrl ?? "",
        CreatedAt = u.CreatedAt.ToString("O"),
        LastLogin = u.LastLogin?.ToString("O") ?? "",
        IsActive = u.IsActive,
        AiProfileDataJson = u.AiProfileDataJson ?? "",
        PreferencesJson = u.PreferencesJson ?? ""
    };
}ParseOptions.0.json£
@/app/src/user-service/Migrations/20260507173016_InitialCreate.cs…using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeycloakId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AiProfileDataJson = table.Column<string>(type: "text", nullable: true),
                    PreferencesJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
ParseOptions.0.jsonµ
I/app/src/user-service/Migrations/20260507173016_InitialCreate.Designer.cs“// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using UserService;

#nullable disable

namespace UserService.Migrations
{
    [DbContext(typeof(UserDbContext))]
    [Migration("20260507173016_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("UserService.Entity.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiProfileDataJson")
                        .HasColumnType("text");

                    b.Property<string>("AvatarUrl")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<DateTime?>("BirthDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("KeycloakId")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<DateTime?>("LastLogin")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("PreferencesJson")
                        .HasColumnType("text");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("users");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.json√
>/app/src/user-service/Migrations/UserDbContextModelSnapshot.csÎ// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using UserService;

#nullable disable

namespace UserService.Migrations
{
    [DbContext(typeof(UserDbContext))]
    partial class UserDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("UserService.Entity.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiProfileDataJson")
                        .HasColumnType("text");

                    b.Property<string>("AvatarUrl")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<DateTime?>("BirthDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("KeycloakId")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<DateTime?>("LastLogin")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("PreferencesJson")
                        .HasColumnType("text");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("users");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.jsonÂ
 /app/src/user-service/Program.cs´using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UserService;
using UserService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<UserDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddGrpc();

builder.Services.AddAuthentication("TokenForward")
    .AddScheme<TokenForwardOptions, TokenForwardHandler>("TokenForward", _ => { });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IKafkaPublisher, KafkaPublisher>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var pending = await dbContext.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            logger.LogInformation("Applying {Count} migrations...", pending.Count());
            await dbContext.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration failed");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<UserService.Grpc.UserServiceImpl>();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "user-service" }));

app.Run();

public class TokenForwardOptions : AuthenticationSchemeOptions { }

public class TokenForwardHandler : AuthenticationHandler<TokenForwardOptions>
{
    public TokenForwardHandler(
        IOptionsMonitor<TokenForwardOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authHeader["Bearer ".Length..];
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            var claims = jwt.Claims.Select(c => new Claim(c.Type, c.Value)).ToList();
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Token parsing failed");
            return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
        }
    }
}
ParseOptions.0.json
0/app/src/user-service/Services/KafkaPublisher.cs¶using System.Text.Json;
using Confluent.Kafka;
using UserService.Events;

namespace UserService.Services;

public interface IKafkaPublisher
{
    Task PublishAsync<T>(T evt, string topic) where T : UserEvent;
}

public class KafkaPublisher : IKafkaPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaPublisher> _logger;

    public KafkaPublisher(IConfiguration configuration, ILogger<KafkaPublisher> logger)
    {
        _logger = logger;

        var bootstrapServers = configuration.GetValue<string>("KAFKA_BOOTSTRAP_SERVERS")
            ?? Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS")
            ?? "kafka:9092";

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.Leader,
            MessageTimeoutMs = 5000,
            RequestTimeoutMs = 5000,
            RetryBackoffMs = 100,
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<T>(T evt, string topic) where T : UserEvent
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = evt switch
                {
                    UserCreatedEvent e => e.UserId.ToString(),
                    _ => evt.EventId.ToString()
                },
                Value = JsonSerializer.Serialize(evt),
                Headers = new Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes(typeof(T).Name) },
                    { "event-id", System.Text.Encoding.UTF8.GetBytes(evt.EventId.ToString()) }
                }
            };

            var deliveryResult = await _producer.ProduceAsync(topic, message);

            _logger.LogInformation("Published {EventType} to {Topic} [offset {Offset}]",
                typeof(T).Name, deliveryResult.Topic, deliveryResult.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish {EventType} to {Topic}: {Reason}",
                typeof(T).Name, topic, ex.Error.Reason);
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
ParseOptions.0.jsonß
&/app/src/user-service/UserDbContext.csÁusing Microsoft.EntityFrameworkCore;
using UserService.Entity;

namespace UserService;

public class UserDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();
    }
}ParseOptions.0.json–
E/app/src/user-service/obj/Debug/net10.0/UserService.GlobalUsings.g.csÒ// <auto-generated/>
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Net.Http.Json;
global using System.Threading;
global using System.Threading.Tasks;
ParseOptions.0.jsonµ
W/app/src/user-service/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.csƒ// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
ParseOptions.0.jsonç
C/app/src/user-service/obj/Debug/net10.0/UserService.AssemblyInfo.cs∞//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UserService")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0")]
[assembly: System.Reflection.AssemblyProductAttribute("UserService")]
[assembly: System.Reflection.AssemblyTitleAttribute("UserService")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json©
V/app/src/user-service/obj/Debug/net10.0/UserService.MvcApplicationPartsAssemblyInfo.csπ//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute("Swashbuckle.AspNetCore.SwaggerGen")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.jsonˇ
?/app/src/user-service/obj/Debug/net10.0/EFCoreNpgsqlPgvector.cs¶//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: Microsoft.EntityFrameworkCore.Design.DesignTimeServicesReferenceAttribute(("Pgvector.EntityFrameworkCore.VectorDesignTimeServices, Pgvector.EntityFrameworkCo" +
    "re"), "Npgsql.EntityFrameworkCore.PostgreSQL")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json˘
π/app/src/user-service/obj/Debug/net10.0/Microsoft.AspNetCore.App.SourceGenerators/Microsoft.AspNetCore.SourceGenerators.PublicProgramSourceGenerator/PublicTopLevelProgram.Generated.g.cs•// <auto-generated />
/// <summary>
/// Auto-generated public partial Program class for top-level statement apps.
/// </summary>
public partial class Program { }ParseOptions.0.json