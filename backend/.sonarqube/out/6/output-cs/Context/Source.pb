ÿ	
4/app/src/application-service/ApplicationDbContext.cs±	using Microsoft.EntityFrameworkCore;
using ApplicationService.Entities;

namespace ApplicationService;

public class ApplicationDbContext : DbContext
{
    public DbSet<Application> Applications { get; set; }
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Application>()
            .Property(a => a.Status)
            .HasConversion<string>();

        modelBuilder.Entity<ApplicationStatusHistory>()
            .Property(h => h.OldStatus)
            .HasConversion<string>();

        modelBuilder.Entity<ApplicationStatusHistory>()
            .Property(h => h.NewStatus)
            .HasConversion<string>();

        modelBuilder.Entity<Application>()
            .HasIndex(a => a.CandidateId);

        modelBuilder.Entity<Application>()
            .HasIndex(a => a.Status);

        modelBuilder.Entity<Application>()
            .HasIndex(a => a.AppliedAt);
    }
}ParseOptions.0.json­%
B/app/src/application-service/Controllers/ApplicationsController.csÑ$using System.ComponentModel.DataAnnotations;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApplicationService.DTOs;
using ApplicationService.Services;
using ApplicationService.Validators;
using FluentValidation;

namespace ApplicationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _service;
    private readonly IValidator<CreateApplicationDto> _createValidator;
    private readonly IValidator<UpdateStatusDto> _statusValidator;
    private readonly IValidator<UpdateApplicationDto> _updateValidator;
    private readonly ILogger<ApplicationsController> _logger;

    public ApplicationsController(
        IApplicationService service,
        IValidator<CreateApplicationDto> createValidator,
        IValidator<UpdateStatusDto> statusValidator,
        IValidator<UpdateApplicationDto> updateValidator,
        ILogger<ApplicationsController> logger)
    {
        _service = service;
        _createValidator = createValidator;
        _statusValidator = statusValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    private string? GetUserId()
    {
        return User.FindFirst("sub")?.Value
            ?? User.FindFirst("local_user_id")?.Value;
    }

    /// GET /applications
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? candidateId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _service.GetAllAsync(candidateId, page, pageSize);
        return Ok(ApiResponse<ApplicationListDto>.Ok(result));
    }

    /// GET /applications/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var app = await _service.GetByIdAsync(id);
        if (app == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
        return Ok(ApiResponse<ApplicationResponseDto>.Ok(app));
    }

    /// POST /applications
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(validation.Errors.First().ErrorMessage));

        var created = await _service.CreateAsync(dto, GetUserId());
        return Created($"/api/applications/{created.Id}", ApiResponse<ApplicationResponseDto>.Created(created));
    }

    /// PATCH /applications/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var validation = await _statusValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(validation.Errors.First().ErrorMessage));

        var updated = await _service.UpdateStatusAsync(id, dto, GetUserId());
        if (updated == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
        return Ok(ApiResponse<ApplicationResponseDto>.Ok(updated));
    }

    /// PUT /applications/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApplicationDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(validation.Errors.First().ErrorMessage));

        var updated = await _service.UpdateDetailsAsync(id, dto, GetUserId());
        if (updated == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
        return Ok(ApiResponse<ApplicationResponseDto>.Ok(updated));
    }

    /// DELETE /applications/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Application not found"));
        return NoContent();
    }

    /// GET /applications/statistics
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] Guid? candidateId)
    {
        var stats = await _service.GetStatisticsAsync(candidateId);
        return Ok(ApiResponse<ApplicationStatisticsDto>.Ok(stats));
    }
}ParseOptions.0.jsonÒ

4/app/src/application-service/DTOs/ApplicationDtos.cs„
namespace ApplicationService.DTOs;

public record ApplicationResponseDto(
    Guid Id,
    Guid CandidateId,
    Guid? CvVersionId,
    Guid? JobOfferId,
    string CompanyName,
    string PositionTitle,
    string? OfferSource,
    string Status,
    DateTime AppliedAt,
    DateTime UpdatedAt,
    string? Notes,
    List<StatusHistoryDto>? History = null
);

public record StatusHistoryDto(
    Guid Id,
    string? OldStatus,
    string NewStatus,
    DateTime ChangedAt,
    string? ChangedBy,
    string? Comment
);

public record CreateApplicationDto(
    Guid CandidateId,
    Guid? CvVersionId,
    Guid? JobOfferId,
    string CompanyName,
    string PositionTitle,
    string? OfferSource,
    string? Notes
);

public record UpdateStatusDto(
    string Status,
    string? Comment
);

public record UpdateApplicationDto(
    string? CompanyName,
    string? PositionTitle,
    string? OfferSource,
    string? Notes
);

public record ApplicationStatisticsDto(
    int Total,
    int Pending,
    int Reviewed,
    int Interview,
    int Accepted,
    int Rejected,
    int Cancelled
);

public record ApplicationListDto(
    List<ApplicationResponseDto> Items,
    int Total,
    int Page,
    int PageSize
);ParseOptions.0.jsonš	
4/app/src/application-service/Entities/Application.csÌusing System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApplicationService.Entities;

[Table("applications")]
public class Application
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CandidateId { get; set; }

    public Guid? CvVersionId { get; set; }

    public Guid? JobOfferId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string CompanyName { get; set; }

    [Required]
    [MaxLength(150)]
    public required string PositionTitle { get; set; }

    [MaxLength(100)]
    public string? OfferSource { get; set; }

    [Required]
    public ApplicationStatus Status { get; set; } = ApplicationStatus.PENDING;

    [Required]
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? Notes { get; set; }

    // Navigation
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
}ParseOptions.0.jsonû
:/app/src/application-service/Entities/ApplicationStatus.cs§namespace ApplicationService.Entities;

public enum ApplicationStatus
{
    PENDING,
    REVIEWED,
    INTERVIEW,
    ACCEPTED,
    REJECTED,
    CANCELLED
}ParseOptions.0.jsonÛ
A/app/src/application-service/Entities/ApplicationStatusHistory.cs€using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApplicationService.Entities;

[Table("application_status_history")]
public class ApplicationStatusHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ApplicationId { get; set; }

    [ForeignKey(nameof(ApplicationId))]
    public Application? Application { get; set; }

    public ApplicationStatus? OldStatus { get; set; }

    [Required]
    public ApplicationStatus NewStatus { get; set; }

    [Required]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ChangedBy { get; set; }

    public string? Comment { get; set; }
}ParseOptions.0.jsonà

8/app/src/application-service/Events/ApplicationEvents.csŽ
namespace ApplicationService.Events;

public abstract record ApplicationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record ApplicationCreatedEvent : ApplicationEvent
{
    public Guid ApplicationId { get; init; }
    public Guid CandidateId { get; init; }
    public string CompanyName { get; init; } = "";
    public string PositionTitle { get; init; } = "";
    public string Status { get; init; } = "PENDING";
}

public record ApplicationStatusUpdatedEvent : ApplicationEvent
{
    public Guid ApplicationId { get; init; }
    public string OldStatus { get; init; } = "";
    public string NewStatus { get; init; } = "";
    public string? ChangedBy { get; init; }
    public string? Comment { get; init; }
}

public record ApplicationDeletedEvent : ApplicationEvent
{
    public Guid ApplicationId { get; init; }
    public Guid CandidateId { get; init; }
}

public record ApplicationUpdatedEvent : ApplicationEvent
{
    public Guid ApplicationId { get; init; }
    public Guid CandidateId { get; init; }
    public string CompanyName { get; init; } = "";
    public string PositionTitle { get; init; } = "";
    public string? UpdatedBy { get; init; }
}ParseOptions.0.jsoní 
G/app/src/application-service/Migrations/20260430154617_InitialCreate.csŒ using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplicationService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CvVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobOfferId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PositionTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OfferSource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "application_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<string>(type: "text", nullable: true),
                    NewStatus = table.Column<string>(type: "text", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_status_history_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_status_history_ApplicationId",
                table: "application_status_history",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_applications_AppliedAt",
                table: "applications",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_applications_CandidateId",
                table: "applications",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_applications_Status",
                table: "applications",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_status_history");

            migrationBuilder.DropTable(
                name: "applications");
        }
    }
}
ParseOptions.0.jsonÃ%
P/app/src/application-service/Migrations/20260430154617_InitialCreate.Designer.csÙ$// <auto-generated />
using System;
using ApplicationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApplicationService.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260430154617_InitialCreate")]
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

            modelBuilder.Entity("ApplicationService.Entities.Application", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("AppliedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("CandidateId")
                        .HasColumnType("uuid");

                    b.Property<string>("CompanyName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<Guid?>("CvVersionId")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("JobOfferId")
                        .HasColumnType("uuid");

                    b.Property<string>("Notes")
                        .HasColumnType("text");

                    b.Property<string>("OfferSource")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("PositionTitle")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AppliedAt");

                    b.HasIndex("CandidateId");

                    b.HasIndex("Status");

                    b.ToTable("applications");
                });

            modelBuilder.Entity("ApplicationService.Entities.ApplicationStatusHistory", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("ApplicationId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("ChangedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ChangedBy")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("Comment")
                        .HasColumnType("text");

                    b.Property<string>("NewStatus")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("OldStatus")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("ApplicationId");

                    b.ToTable("application_status_history");
                });

            modelBuilder.Entity("ApplicationService.Entities.ApplicationStatusHistory", b =>
                {
                    b.HasOne("ApplicationService.Entities.Application", "Application")
                        .WithMany("StatusHistory")
                        .HasForeignKey("ApplicationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Application");
                });

            modelBuilder.Entity("ApplicationService.Entities.Application", b =>
                {
                    b.Navigation("StatusHistory");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.jsonß$
L/app/src/application-service/Migrations/ApplicationDbContextModelSnapshot.csù#// <auto-generated />
using System;
using ApplicationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApplicationService.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("ApplicationService.Entities.Application", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("AppliedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("CandidateId")
                        .HasColumnType("uuid");

                    b.Property<string>("CompanyName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<Guid?>("CvVersionId")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("JobOfferId")
                        .HasColumnType("uuid");

                    b.Property<string>("Notes")
                        .HasColumnType("text");

                    b.Property<string>("OfferSource")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("PositionTitle")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AppliedAt");

                    b.HasIndex("CandidateId");

                    b.HasIndex("Status");

                    b.ToTable("applications");
                });

            modelBuilder.Entity("ApplicationService.Entities.ApplicationStatusHistory", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("ApplicationId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("ChangedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ChangedBy")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("Comment")
                        .HasColumnType("text");

                    b.Property<string>("NewStatus")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("OldStatus")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("ApplicationId");

                    b.ToTable("application_status_history");
                });

            modelBuilder.Entity("ApplicationService.Entities.ApplicationStatusHistory", b =>
                {
                    b.HasOne("ApplicationService.Entities.Application", "Application")
                        .WithMany("StatusHistory")
                        .HasForeignKey("ApplicationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Application");
                });

            modelBuilder.Entity("ApplicationService.Entities.Application", b =>
                {
                    b.Navigation("StatusHistory");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.jsoné
'/app/src/application-service/Program.cs¨using Microsoft.EntityFrameworkCore;
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

app.Run();ParseOptions.0.json°$
B/app/src/application-service/Repositories/ApplicationRepository.csÔ#using Microsoft.EntityFrameworkCore;
using ApplicationService.DTOs;
using ApplicationService.Entities;

namespace ApplicationService.Repositories;

public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(Guid id);
    Task<Application?> GetByIdWithHistoryAsync(Guid id);
    Task<List<Application>> GetAllAsync(Guid? candidateId, int page, int pageSize);
    Task<int> GetTotalCountAsync(Guid? candidateId);
    Task<Application> CreateAsync(Application application);
    Task<Application> UpdateAsync(Application application);
    Task<Application> UpdateDetailsAsync(Guid id, UpdateApplicationDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<Dictionary<ApplicationStatus, int>> GetStatisticsAsync(Guid? candidateId);
}

public class ApplicationRepository : IApplicationRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ApplicationRepository> _logger;

    public ApplicationRepository(ApplicationDbContext db, ILogger<ApplicationRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Application?> GetByIdAsync(Guid id)
        => await _db.Applications.FindAsync(id);

    public async Task<Application?> GetByIdWithHistoryAsync(Guid id)
        => await _db.Applications
            .Include(a => a.StatusHistory.OrderByDescending(h => h.ChangedAt))
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<List<Application>> GetAllAsync(Guid? candidateId, int page, int pageSize)
    {
        var query = _db.Applications.AsQueryable();

        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);

        return await query
            .OrderByDescending(a => a.AppliedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(Guid? candidateId)
    {
        var query = _db.Applications.AsQueryable();
        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);
        return await query.CountAsync();
    }

    public async Task<Application> CreateAsync(Application application)
    {
        _db.Applications.Add(application);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created application {Id}", application.Id);
        return application;
    }

    public async Task<Application> UpdateAsync(Application application)
    {
        application.UpdatedAt = DateTime.UtcNow;
        _db.Applications.Update(application);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated application {Id}", application.Id);
        return application;
    }

    public async Task<Application> UpdateDetailsAsync(Guid id, UpdateApplicationDto dto)
    {
        var application = await _db.Applications.FindAsync(id);
        if (application == null) throw new KeyNotFoundException($"Application {id} not found");

        if (dto.CompanyName != null) application.CompanyName = dto.CompanyName;
        if (dto.PositionTitle != null) application.PositionTitle = dto.PositionTitle;
        if (dto.OfferSource != null) application.OfferSource = dto.OfferSource;
        if (dto.Notes != null) application.Notes = dto.Notes;

        application.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated application details {Id}", id);
        return application;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var application = await _db.Applications.FindAsync(id);
        if (application == null) return false;

        _db.Applications.Remove(application);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted application {Id}", id);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
        => await _db.Applications.AnyAsync(a => a.Id == id);

    public async Task<Dictionary<ApplicationStatus, int>> GetStatisticsAsync(Guid? candidateId)
    {
        var query = _db.Applications.AsQueryable();
        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);

        return await query
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }
}ParseOptions.0.jsonÆ
O/app/src/application-service/Repositories/ApplicationStatusHistoryRepository.csÝ
using Microsoft.EntityFrameworkCore;
using ApplicationService.Entities;

namespace ApplicationService.Repositories;

public interface IApplicationStatusHistoryRepository
{
    Task<ApplicationStatusHistory> CreateAsync(ApplicationStatusHistory history);
    Task<List<ApplicationStatusHistory>> GetByApplicationIdAsync(Guid applicationId);
}

public class ApplicationStatusHistoryRepository : IApplicationStatusHistoryRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ApplicationStatusHistoryRepository> _logger;

    public ApplicationStatusHistoryRepository(ApplicationDbContext db, ILogger<ApplicationStatusHistoryRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ApplicationStatusHistory> CreateAsync(ApplicationStatusHistory history)
    {
        _db.ApplicationStatusHistory.Add(history);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created status history for application {AppId}", history.ApplicationId);
        return history;
    }

    public async Task<List<ApplicationStatusHistory>> GetByApplicationIdAsync(Guid applicationId)
        => await _db.ApplicationStatusHistory
            .Where(h => h.ApplicationId == applicationId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
}ParseOptions.0.json¯B
?/app/src/application-service/Services/ApplicationServiceImpl.csÖAusing ApplicationService.DTOs;
using ApplicationService.Entities;
using ApplicationService.Events;
using ApplicationService.Repositories;

namespace ApplicationService.Services;

public interface IApplicationService
{
    Task<ApplicationListDto> GetAllAsync(Guid? candidateId, int page, int pageSize);
    Task<ApplicationResponseDto?> GetByIdAsync(Guid id);
    Task<ApplicationResponseDto> CreateAsync(CreateApplicationDto dto, string? userId);
    Task<ApplicationResponseDto?> UpdateStatusAsync(Guid id, UpdateStatusDto dto, string? userId);
    Task<ApplicationResponseDto?> UpdateDetailsAsync(Guid id, UpdateApplicationDto dto, string? userId);
    Task<bool> DeleteAsync(Guid id);
    Task<ApplicationStatisticsDto> GetStatisticsAsync(Guid? candidateId);
}

public class ApplicationServiceImpl : IApplicationService
{
    private readonly IApplicationRepository _appRepo;
    private readonly IApplicationStatusHistoryRepository _historyRepo;
    private readonly IKafkaPublisher _kafkaPublisher;
    private readonly ILogger<ApplicationServiceImpl> _logger;

    public ApplicationServiceImpl(
        IApplicationRepository appRepo,
        IApplicationStatusHistoryRepository historyRepo,
        IKafkaPublisher kafkaPublisher,
        ILogger<ApplicationServiceImpl> logger)
    {
        _appRepo = appRepo;
        _historyRepo = historyRepo;
        _kafkaPublisher = kafkaPublisher;
        _logger = logger;
    }

    public async Task<ApplicationListDto> GetAllAsync(Guid? candidateId, int page, int pageSize)
    {
        var apps = await _appRepo.GetAllAsync(candidateId, page, pageSize);
        var total = await _appRepo.GetTotalCountAsync(candidateId);

        return new ApplicationListDto(
            apps.Select(MapToDto).ToList(),
            total,
            page,
            pageSize
        );
    }

    public async Task<ApplicationResponseDto?> GetByIdAsync(Guid id)
    {
        var app = await _appRepo.GetByIdWithHistoryAsync(id);
        return app == null ? null : MapToDtoWithHistory(app);
    }

    public async Task<ApplicationResponseDto> CreateAsync(CreateApplicationDto dto, string? userId)
    {
        var application = new Application
        {
            CandidateId = dto.CandidateId,
            CvVersionId = dto.CvVersionId,
            JobOfferId = dto.JobOfferId,
            CompanyName = dto.CompanyName,
            PositionTitle = dto.PositionTitle,
            OfferSource = dto.OfferSource,
            Status = ApplicationStatus.PENDING,
            AppliedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Notes = dto.Notes
        };

        var created = await _appRepo.CreateAsync(application);

        // Record initial history
        await _historyRepo.CreateAsync(new ApplicationStatusHistory
        {
            ApplicationId = created.Id,
            OldStatus = null,
            NewStatus = ApplicationStatus.PENDING,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = userId,
            Comment = "Application created"
        });

        _logger.LogInformation("Application created {Id} by user {User}", created.Id, userId);

        // Emit event (event-ready architecture)
        var evt = new ApplicationCreatedEvent
        {
            ApplicationId = created.Id,
            CandidateId = created.CandidateId,
            CompanyName = created.CompanyName,
            PositionTitle = created.PositionTitle,
            Status = created.Status.ToString()
        };

        await PublishEvent(evt);

        return MapToDto(created);
    }

    public async Task<ApplicationResponseDto?> UpdateStatusAsync(Guid id, UpdateStatusDto dto, string? userId)
    {
        var app = await _appRepo.GetByIdAsync(id);
        if (app == null) return null;

        var oldStatus = app.Status;
        var newStatus = Enum.Parse<ApplicationStatus>(dto.Status.ToUpperInvariant());

        app.Status = newStatus;
        app.UpdatedAt = DateTime.UtcNow;

        await _appRepo.UpdateAsync(app);

        // Record history
        await _historyRepo.CreateAsync(new ApplicationStatusHistory
        {
            ApplicationId = app.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = userId,
            Comment = dto.Comment
        });

        _logger.LogInformation("Application {Id} status updated from {Old} to {New} by {User}",
            id, oldStatus, newStatus, userId);

        var evt = new ApplicationStatusUpdatedEvent
        {
            ApplicationId = app.Id,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            ChangedBy = userId,
            Comment = dto.Comment
        };

        await PublishEvent(evt);

        var result = await _appRepo.GetByIdWithHistoryAsync(id);
        return result == null ? null : MapToDtoWithHistory(result);
    }

    public async Task<ApplicationResponseDto?> UpdateDetailsAsync(Guid id, UpdateApplicationDto dto, string? userId)
    {
        try
        {
            var updated = await _appRepo.UpdateDetailsAsync(id, dto);

            var evt = new ApplicationUpdatedEvent
            {
                ApplicationId = updated.Id,
                CandidateId = updated.CandidateId,
                CompanyName = updated.CompanyName,
                PositionTitle = updated.PositionTitle,
                UpdatedBy = userId
            };

            await PublishEvent(evt);

            var result = await _appRepo.GetByIdWithHistoryAsync(id);
            return result == null ? null : MapToDtoWithHistory(result);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var app = await _appRepo.GetByIdAsync(id);
        if (app == null) return false;

        var evt = new ApplicationDeletedEvent
        {
            ApplicationId = id,
            CandidateId = app.CandidateId
        };

        var deleted = await _appRepo.DeleteAsync(id);

        if (deleted)
        {
            _logger.LogInformation("Application {Id} deleted", id);
            await PublishEvent(evt);
        }

        return deleted;
    }

    public async Task<ApplicationStatisticsDto> GetStatisticsAsync(Guid? candidateId)
    {
        var stats = await _appRepo.GetStatisticsAsync(candidateId);

        return new ApplicationStatisticsDto(
            stats.Values.Sum(),
            stats.GetValueOrDefault(ApplicationStatus.PENDING, 0),
            stats.GetValueOrDefault(ApplicationStatus.REVIEWED, 0),
            stats.GetValueOrDefault(ApplicationStatus.INTERVIEW, 0),
            stats.GetValueOrDefault(ApplicationStatus.ACCEPTED, 0),
            stats.GetValueOrDefault(ApplicationStatus.REJECTED, 0),
            stats.GetValueOrDefault(ApplicationStatus.CANCELLED, 0)
        );
    }

    private static ApplicationResponseDto MapToDto(Application a) => new(
        a.Id, a.CandidateId, a.CvVersionId, a.JobOfferId,
        a.CompanyName, a.PositionTitle, a.OfferSource,
        a.Status.ToString(), a.AppliedAt, a.UpdatedAt, a.Notes
    );

    private static ApplicationResponseDto MapToDtoWithHistory(Application a) => new(
        a.Id, a.CandidateId, a.CvVersionId, a.JobOfferId,
        a.CompanyName, a.PositionTitle, a.OfferSource,
        a.Status.ToString(), a.AppliedAt, a.UpdatedAt, a.Notes,
        a.StatusHistory?.Select(h => new StatusHistoryDto(
            h.Id, h.OldStatus?.ToString(), h.NewStatus.ToString(),
            h.ChangedAt, h.ChangedBy, h.Comment
        )).ToList()
    );

    private async Task PublishEvent(ApplicationEvent evt)
    {
        var topic = evt switch
        {
            ApplicationCreatedEvent => "application.created",
            ApplicationStatusUpdatedEvent => "application.status.updated",
            ApplicationUpdatedEvent => "application.updated",
            ApplicationDeletedEvent => "application.deleted",
            _ => throw new ArgumentException("Unknown event type")
        };

        await _kafkaPublisher.PublishAsync(evt, topic);
    }
}ParseOptions.0.json¬
7/app/src/application-service/Services/KafkaPublisher.csÛusing System.Text.Json;
using Confluent.Kafka;
using ApplicationService.Events;

namespace ApplicationService.Services;

public interface IKafkaPublisher
{
    Task PublishAsync<T>(T evt, string topic) where T : ApplicationEvent;
}

public class KafkaPublisher : IKafkaPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaPublisher> _logger;

    public KafkaPublisher(IConfiguration configuration, ILogger<KafkaPublisher> logger)
    {
        _logger = logger;

        var bootstrapServers = configuration.GetValue<string>("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092";

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

    public async Task PublishAsync<T>(T evt, string topic) where T : ApplicationEvent
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = GetKey(evt),
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

    private static string GetKey(ApplicationEvent evt) => evt switch
    {
        ApplicationCreatedEvent e => e.ApplicationId.ToString(),
        ApplicationStatusUpdatedEvent e => e.ApplicationId.ToString(),
        ApplicationUpdatedEvent e => e.ApplicationId.ToString(),
        ApplicationDeletedEvent e => e.ApplicationId.ToString(),
        _ => evt.EventId.ToString()
    };

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
ParseOptions.0.jsonÕ
@/app/src/application-service/Validators/ApplicationValidators.csûusing FluentValidation;
using ApplicationService.DTOs;

namespace ApplicationService.Validators;

public class CreateApplicationValidator : AbstractValidator<CreateApplicationDto>
{
    public CreateApplicationValidator()
    {
        RuleFor(x => x.CandidateId)
            .NotEmpty().WithMessage("CandidateId is required");

        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required")
            .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters");

        RuleFor(x => x.PositionTitle)
            .NotEmpty().WithMessage("Position title is required")
            .MaximumLength(150).WithMessage("Position title cannot exceed 150 characters");

        RuleFor(x => x.OfferSource)
            .MaximumLength(100).WithMessage("Offer source cannot exceed 100 characters");
    }
}

public class UpdateApplicationValidator : AbstractValidator<UpdateApplicationDto>
{
    public UpdateApplicationValidator()
    {
        RuleFor(x => x.CompanyName)
            .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters")
            .When(x => x.CompanyName != null);

        RuleFor(x => x.PositionTitle)
            .MaximumLength(150).WithMessage("Position title cannot exceed 150 characters")
            .When(x => x.PositionTitle != null);

        RuleFor(x => x.OfferSource)
            .MaximumLength(100).WithMessage("Offer source cannot exceed 100 characters")
            .When(x => x.OfferSource != null);
    }
}

public class UpdateStatusValidator : AbstractValidator<UpdateStatusDto>
{
    public UpdateStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required")
            .Must(BeValidStatus).WithMessage("Invalid status value. Valid values: PENDING, REVIEWED, INTERVIEW, ACCEPTED, REJECTED, CANCELLED");
    }

    private static bool BeValidStatus(string status)
    {
        var validStatuses = new[] { "PENDING", "REVIEWED", "INTERVIEW", "ACCEPTED", "REJECTED", "CANCELLED" };
        return validStatuses.Contains(status.ToUpperInvariant());
    }
}ParseOptions.0.jsonÞ
S/app/src/application-service/obj/Debug/net10.0/ApplicationService.GlobalUsings.g.csñ// <auto-generated/>
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
ParseOptions.0.json¼
^/app/src/application-service/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.csÄ// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
ParseOptions.0.json°
Q/app/src/application-service/obj/Debug/net10.0/ApplicationService.AssemblyInfo.csÅ//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("ApplicationService")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0")]
[assembly: System.Reflection.AssemblyProductAttribute("ApplicationService")]
[assembly: System.Reflection.AssemblyTitleAttribute("ApplicationService")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json¥
d/app/src/application-service/obj/Debug/net10.0/ApplicationService.MvcApplicationPartsAssemblyInfo.cs§//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute("FluentValidation.AspNetCore")]
[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute("Swashbuckle.AspNetCore.SwaggerGen")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json€
À/app/src/application-service/obj/Debug/net10.0/Microsoft.AspNetCore.App.SourceGenerators/Microsoft.AspNetCore.SourceGenerators.PublicProgramSourceGenerator/PublicTopLevelProgram.Generated.g.cs¥// <auto-generated />
/// <summary>
/// Auto-generated public partial Program class for top-level statement apps.
/// </summary>
public partial class Program { }ParseOptions.0.json