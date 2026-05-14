�:
</app/src/job-offer-service/controllers/JobOfferController.cs�9using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobOfferService.DTOs;
using JobOfferService.Services;
using JobOfferService.Validators;
using CVGenerator.Shared;
using FluentValidation;

namespace JobOfferService.Controllers;

[ApiController]
[Route("api/v1/job-offers")]
// [Authorize] // Uncomment this if the service requires JWT authentication
public class JobOffersController : ControllerBase
{
    private readonly IJobOfferService _service;
    private readonly IValidator<SubmitJobOfferDto> _submitValidator;
    private readonly IValidator<ExtractedJobDto> _extractedValidator;
    private readonly IValidator<UpdateJobStatusDto> _statusValidator;
    private readonly ILogger<JobOffersController> _logger;

    public JobOffersController(
        IJobOfferService service,
        IValidator<SubmitJobOfferDto> submitValidator,
        IValidator<ExtractedJobDto> extractedValidator,
        IValidator<UpdateJobStatusDto> statusValidator,
        ILogger<JobOffersController> logger)
    {
        _service = service;
        _submitValidator = submitValidator;
        _extractedValidator = extractedValidator;
        _statusValidator = statusValidator;
        _logger = logger;
    }

    private string? GetUserId()
    {
        // Extracts the User ID from the JWT Token
        return User.FindFirst("sub")?.Value 
            ?? User.FindFirst("local_user_id")?.Value;
    }

    /// <summary>
    /// Retrieves a paginated list of job offers for a specific user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<JobOfferListDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // If you want to force the user ID from the token instead of query:
        // var currentUserId = Guid.Parse(GetUserId() ?? userId.ToString());

        var result = await _service.GetAllAsync(userId, page, pageSize);
        return Ok(ApiResponse<JobOfferListDto>.Ok(result));
    }

    /// <summary>
    /// Retrieves the full details of a specific job offer, including extracted skills.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<JobOfferDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var job = await _service.GetByIdAsync(id);
        if (job == null) 
        {
            _logger.LogWarning("Job offer with ID {Id} was not found.", id);
            return NotFound(ApiResponse<JobOfferDetailDto>.Error("Job offer not found"));
        }
        return Ok(ApiResponse<JobOfferDetailDto>.Ok(job));
    }

    /// <summary>
    /// Submits a raw job description or LinkedIn URL for AI processing.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> SubmitJob([FromBody] SubmitJobOfferDto dto)
    {
        var validation = await _submitValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Validation failed for SubmitJobOfferDto: {Errors}", validation.Errors);
            return BadRequest(ApiResponse<Guid>.Error(validation.Errors.First().ErrorMessage));
        }

        var createdId = await _service.SubmitRawJobOfferAsync(dto);
        _logger.LogInformation("Successfully submitted raw job offer with ID {Id}", createdId);
        
        return Created($"/api/v1/job-offers/{createdId}", ApiResponse<Guid>.Created(createdId));
    }

    /// <summary>
    /// Callback endpoint for the AI Agent to post the extracted JSON data.
    /// </summary>
    [HttpPost("{id:guid}/extracted")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> ProcessExtractedData(Guid id, [FromBody] ExtractedJobDto dto)
    {
        var validation = await _extractedValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<string>.Error(validation.Errors.First().ErrorMessage));
        }

        try
        {
            await _service.ProcessExtractedDataAsync(id, dto);
            _logger.LogInformation("Successfully processed AI extracted data for job offer {Id}", id);
            return Ok(ApiResponse<string>.Ok("Job offer data extracted and saved successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Failed to process extracted data. Job {Id} not found.", id);
            return NotFound(ApiResponse<string>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Updates the status of a job offer (e.g., DRAFT, OPEN, CLOSED).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateJobStatusDto dto)
    {
        var validation = await _statusValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<string>.Error(validation.Errors.First().ErrorMessage));
        }

        try
        {
            await _service.UpdateStatusAsync(id, dto);
            _logger.LogInformation("Updated status to {Status} for job offer {Id}", dto.Status, id);
            return Ok(ApiResponse<string>.Ok("Status updated successfully."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<string>.Error("Job offer not found"));
        }
    }

    /// <summary>
    /// Deletes a job offer entirely.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        // Added a check so you don't return 204 if it didn't exist
        var jobExists = await _service.GetByIdAsync(id);
        if (jobExists == null)
        {
             return NotFound(ApiResponse<object>.Error("Job offer not found"));
        }

        await _service.DeleteAsync(id);
        _logger.LogInformation("Deleted job offer {Id}", id);
        return NoContent();
    }

    /// <summary>
    /// Retrieves statistics for the user's dashboard.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<JobOfferStatisticsDto>), 200)]
    public async Task<IActionResult> GetStatistics([FromQuery] Guid userId)
    {
        if (userId == Guid.Empty) 
        {
            return BadRequest(ApiResponse<JobOfferStatisticsDto>.Error("userId query parameter is required."));
        }

        var stats = await _service.GetStatisticsAsync(userId);
        return Ok(ApiResponse<JobOfferStatisticsDto>.Ok(stats));
    }
}ParseOptions.0.json�
//app/src/job-offer-service/DTOs/JobOfferDtos.cs�namespace JobOfferService.DTOs;

// ─── RESPONSES (OUTPUTS) ──────────────────────────────────────────────
public record JobOfferDetailDto(
    Guid Id,
    Guid UserId,
    string EnterpriseName,
    string? EnterpriseDescription,
    string JobRole,
    string RawDescription,
    int? RequiredExperienceYears,
    string? SeniorityLevel,
    string? EmploymentType,
    string? Location,
    string? LocationType,
    string? EducationRequirements,
    string? SourceUrl,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    
    // Nested collections for the detailed view
    List<JobSkillDto> Skills,
    List<JobResponsibilityDto> Responsibilities,
    List<JobBenefitDto> Benefits
);

// Child DTOs needed for the detailed view
public record JobSkillDto(
    Guid Id, 
    string Name, 
    string Type, 
    bool IsMandatory
);

public record JobResponsibilityDto(
    Guid Id, 
    string Description
);

public record JobBenefitDto(
    Guid Id, 
    string Description
);
public record JobOfferResponseDto(
    Guid Id,
    Guid UserId,
    string EnterpriseName,
    string? EnterpriseDescription,
    string JobRole,
    string RawDescription,
    int? RequiredExperienceYears,
    string? SeniorityLevel,
    string? EmploymentType,
    string? Location,
    string? LocationType,
    string? EducationRequirements,
    string? SourceUrl,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // Nested lists are optional so we don't over-fetch data if we don't need it
    List<JobSkillDto>? Skills = null,
    List<JobResponsibilityDto>? Responsibilities = null,
    List<JobBenefitDto>? Benefits = null
);

public record JobOfferSummaryDto(
    Guid Id,
    Guid UserId,
    string EnterpriseName,
    string JobRole,
    string? Location,
    string Status,
    DateTime CreatedAt
);

public record JobOfferListDto(
    List<JobOfferSummaryDto> Items,
    int Total,
    int Page,
    int PageSize
);

public record JobOfferStatisticsDto(
    int Total,
    int Draft,
    int Open,
    int Closed,
    int Archived
);


// ─── REQUESTS (INPUTS) ────────────────────────────────────────────────

// 1. What the user sends when they paste a link or raw text into the UI
public record SubmitJobOfferDto(
    Guid UserId,
    string? RawText,
    string? SourceUrl
);

// 2. What the AI Agent sends back after parsing the text
public record ExtractedJobDto(
    string EnterpriseName,
    string? EnterpriseDescription,
    string JobRole,
    string RawDescription,
    List<string> Responsibilities,
    List<string> RequiredSkills,
    List<string> SoftSkills,
    int? RequiredExperienceYears,
    string? SeniorityLevel,
    string? EmploymentType,
    string? Location,
    string? LocationType,
    string? EducationRequirements,
    List<string> Benefits,
    string? SourceUrl
);



// 3. For partial updates by the user (if they want to manually edit a typo)
public record UpdateJobOfferDto(
    string? EnterpriseName,
    string? JobRole,
    string? Location,
    string? EmploymentType,
    int? RequiredExperienceYears
);

// 4. For updating the state of the job offer
public record UpdateJobStatusDto(
    string Status
);ParseOptions.0.json�
1/app/src/job-offer-service/Entities/JobBenefit.cs�namespace JobOfferService.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("job_benefits")]
public class JobBenefit
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid JobOfferId { get; set; }

    [Required]
    [MaxLength(300)]
    public required string Description { get; set; }

    // Navigation
    [ForeignKey(nameof(JobOfferId))]
    public JobOffer? JobOffer { get; set; }
}ParseOptions.0.json�
//app/src/job-offer-service/Entities/JobOffer.cs�using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector; // Required for pgvector support


namespace JobOfferService.Entities;

[Table("job_offers")]
public class JobOffer
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string EnterpriseName { get; set; }

    public string? EnterpriseDescription { get; set; }

    [Required]
    [MaxLength(150)]
    public required string JobRole { get; set; }

    [Required]
    public required string RawDescription { get; set; }

    // Maps to pgvector extension in PostgreSQL
    [Column(TypeName = "vector(1536)")] 
    public Vector? DescriptionVector { get; set; }

    public int? RequiredExperienceYears { get; set; }

    [MaxLength(100)]
    public string? SeniorityLevel { get; set; }

    [MaxLength(100)]
    public string? EmploymentType { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(100)]
    public string? LocationType { get; set; }

    public string? EducationRequirements { get; set; }

    [MaxLength(500)]
    public string? SourceUrl { get; set; }
    
    [Required]
    public JobOfferStatus Status { get; set; } = JobOfferStatus.OPEN;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<JobSkill> Skills { get; set; } = new List<JobSkill>();
    public ICollection<JobResponsibility> Responsibilities { get; set; } = new List<JobResponsibility>();
    public ICollection<JobBenefit> Benefits { get; set; } = new List<JobBenefit>();
}

ParseOptions.0.json�
5/app/src/job-offer-service/Entities/JobOfferStatus.cs{namespace JobOfferService.Entities;

public enum JobOfferStatus
{
    DRAFT,
    OPEN,
    CLOSED,
    ARCHIVED
}
ParseOptions.0.json�
8/app/src/job-offer-service/Entities/JobResponsibility.cs�namespace JobOfferService.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("job_responsibilities")]
public class JobResponsibility
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid JobOfferId { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Description { get; set; }

    // Navigation
    [ForeignKey(nameof(JobOfferId))]
    public JobOffer? JobOffer { get; set; }
}ParseOptions.0.json�
//app/src/job-offer-service/Entities/JobSkill.cs�
namespace JobOfferService.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("job_skills")]
public class JobSkill
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid JobOfferId { get; set; }

    [Required]
    [MaxLength(150)]
    public required string Name { get; set; }

    [Required]
    public SkillType Type { get; set; }

    [Required]
    public bool IsMandatory { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(JobOfferId))]
    public JobOffer? JobOffer { get; set; }
}ParseOptions.0.json�
0/app/src/job-offer-service/Entities/SkillType.cs�namespace JobOfferService.Entities;

public enum SkillType
{
    HARD_SKILL,
    SOFT_SKILL,
    LANGUAGE,
    CERTIFICATION
}ParseOptions.0.json�

//app/src/job-offer-service/JobOfferDbContext.cs�
using Microsoft.EntityFrameworkCore;
using JobOfferService.Entities;
using Pgvector.EntityFrameworkCore; // Required for pgvector


public class JobOfferDbContext : DbContext
{
    public JobOfferDbContext(DbContextOptions<JobOfferDbContext> options) : base(options) { }

    public DbSet<JobOffer> JobOffers { get; set; }
    public DbSet<JobSkill> JobSkills { get; set; }
    public DbSet<JobResponsibility> JobResponsibilities { get; set; }
    public DbSet<JobBenefit> JobBenefits { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. CRITICAL: Tell PostgreSQL to enable the vector extension
        modelBuilder.HasPostgresExtension("vector");

        // 2. Configure the JobOffer table and its relationships
        modelBuilder.Entity<JobOffer>(entity =>
        {
            entity.HasMany(j => j.Skills).WithOne(s => s.JobOffer).HasForeignKey(s => s.JobOfferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(j => j.Responsibilities).WithOne(r => r.JobOffer).HasForeignKey(r => r.JobOfferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(j => j.Benefits).WithOne(b => b.JobOffer).HasForeignKey(b => b.JobOfferId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}ParseOptions.0.json�3
E/app/src/job-offer-service/Migrations/20260511120331_InitialCreate.cs�2using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace job_offer_service.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "job_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnterpriseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnterpriseDescription = table.Column<string>(type: "text", nullable: true),
                    JobRole = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RawDescription = table.Column<string>(type: "text", nullable: false),
                    DescriptionVector = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    RequiredExperienceYears = table.Column<int>(type: "integer", nullable: true),
                    SeniorityLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LocationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EducationRequirements = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_offers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_benefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_benefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_benefits_job_offers_JobOfferId",
                        column: x => x.JobOfferId,
                        principalTable: "job_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_responsibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_responsibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_responsibilities_job_offers_JobOfferId",
                        column: x => x.JobOfferId,
                        principalTable: "job_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_skills_job_offers_JobOfferId",
                        column: x => x.JobOfferId,
                        principalTable: "job_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_benefits_JobOfferId",
                table: "job_benefits",
                column: "JobOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_job_responsibilities_JobOfferId",
                table: "job_responsibilities",
                column: "JobOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_job_skills_JobOfferId",
                table: "job_skills",
                column: "JobOfferId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_benefits");

            migrationBuilder.DropTable(
                name: "job_responsibilities");

            migrationBuilder.DropTable(
                name: "job_skills");

            migrationBuilder.DropTable(
                name: "job_offers");
        }
    }
}
ParseOptions.0.json�<
N/app/src/job-offer-service/Migrations/20260511120331_InitialCreate.Designer.cs�;// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace job_offer_service.Migrations
{
    [DbContext(typeof(JobOfferDbContext))]
    [Migration("20260511120331_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("JobOfferService.Entities.JobBenefit", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<Guid>("JobOfferId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("JobOfferId");

                    b.ToTable("job_benefits");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobOffer", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Vector>("DescriptionVector")
                        .HasColumnType("vector(1536)");

                    b.Property<string>("EducationRequirements")
                        .HasColumnType("text");

                    b.Property<string>("EmploymentType")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("EnterpriseDescription")
                        .HasColumnType("text");

                    b.Property<string>("EnterpriseName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("JobRole")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Location")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("LocationType")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("RawDescription")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int?>("RequiredExperienceYears")
                        .HasColumnType("integer");

                    b.Property<string>("SeniorityLevel")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("SourceUrl")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.ToTable("job_offers");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobResponsibility", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Guid>("JobOfferId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("JobOfferId");

                    b.ToTable("job_responsibilities");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobSkill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("IsMandatory")
                        .HasColumnType("boolean");

                    b.Property<Guid>("JobOfferId")
                        .HasColumnType("uuid");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("JobOfferId");

                    b.ToTable("job_skills");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobBenefit", b =>
                {
                    b.HasOne("JobOfferService.Entities.JobOffer", "JobOffer")
                        .WithMany("Benefits")
                        .HasForeignKey("JobOfferId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("JobOffer");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobResponsibility", b =>
                {
                    b.HasOne("JobOfferService.Entities.JobOffer", "JobOffer")
                        .WithMany("Responsibilities")
                        .HasForeignKey("JobOfferId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("JobOffer");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobSkill", b =>
                {
                    b.HasOne("JobOfferService.Entities.JobOffer", "JobOffer")
                        .WithMany("Skills")
                        .HasForeignKey("JobOfferId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("JobOffer");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobOffer", b =>
                {
                    b.Navigation("Benefits");

                    b.Navigation("Responsibilities");

                    b.Navigation("Skills");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.json�;
G/app/src/job-offer-service/Migrations/JobOfferDbContextModelSnapshot.cs�;// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace job_offer_service.Migrations
{
    [DbContext(typeof(JobOfferDbContext))]
    partial class JobOfferDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("JobOfferService.Entities.JobBenefit", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<Guid>("JobOfferId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("JobOfferId");

                    b.ToTable("job_benefits");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobOffer", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Vector>("DescriptionVector")
                        .HasColumnType("vector(1536)");

                    b.Property<string>("EducationRequirements")
                        .HasColumnType("text");

                    b.Property<string>("EmploymentType")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("EnterpriseDescription")
                        .HasColumnType("text");

                    b.Property<string>("EnterpriseName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("JobRole")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Location")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("LocationType")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("RawDescription")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int?>("RequiredExperienceYears")
                        .HasColumnType("integer");

                    b.Property<string>("SeniorityLevel")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("SourceUrl")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.ToTable("job_offers");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobResponsibility", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Guid>("JobOfferId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("JobOfferId");

                    b.ToTable("job_responsibilities");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobSkill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("IsMandatory")
                        .HasColumnType("boolean");

                    b.Property<Guid>("JobOfferId")
                        .HasColumnType("uuid");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("JobOfferId");

                    b.ToTable("job_skills");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobBenefit", b =>
                {
                    b.HasOne("JobOfferService.Entities.JobOffer", "JobOffer")
                        .WithMany("Benefits")
                        .HasForeignKey("JobOfferId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("JobOffer");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobResponsibility", b =>
                {
                    b.HasOne("JobOfferService.Entities.JobOffer", "JobOffer")
                        .WithMany("Responsibilities")
                        .HasForeignKey("JobOfferId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("JobOffer");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobSkill", b =>
                {
                    b.HasOne("JobOfferService.Entities.JobOffer", "JobOffer")
                        .WithMany("Skills")
                        .HasForeignKey("JobOfferId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("JobOffer");
                });

            modelBuilder.Entity("JobOfferService.Entities.JobOffer", b =>
                {
                    b.Navigation("Benefits");

                    b.Navigation("Responsibilities");

                    b.Navigation("Skills");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.json�
%/app/src/job-offer-service/Program.cs�using Microsoft.EntityFrameworkCore;
using FluentValidation;
using JobOfferService;
using JobOfferService.Services;
using JobOfferService.Repositories;
using JobOfferService.DTOs;
using JobOfferService.Validators;

var builder = WebApplication.CreateBuilder(args);

// 1. Database (Configured with pgvector via the DbContext)
builder.Services.AddDbContext<JobOfferDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, o => o.UseVector());
});

// 2. Repositories
builder.Services.AddScoped<IJobOfferRepository, JobOfferRepository>();
// builder.Services.AddScoped<IKafkaPublisher, KafkaPublisher>(); // Uncomment when Kafka is ready

// 3. Services
builder.Services.AddScoped<IJobOfferService, JobOfferService.Services.JobOfferService>();

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

// 8. Auth (JWT from gateway/keycloak)
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = Environment.GetEnvironmentVariable("JWT_AUTHORITY") ?? "";
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

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
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();ParseOptions.0.json�$
=/app/src/job-offer-service/Repositories/JobOfferRepository.cs�#using Microsoft.EntityFrameworkCore;
using JobOfferService.Entities;


namespace JobOfferService.Repositories;

public interface IJobOfferRepository
{
    Task<JobOffer?> GetByIdAsync(Guid id);
    Task<JobOffer?> GetByIdWithDetailsAsync(Guid id);
    Task<List<JobOffer>> GetAllAsync(Guid? userId, int page, int pageSize);
    Task<int> GetTotalCountAsync(Guid? userId);
    Task<JobOffer> CreateAsync(JobOffer jobOffer);
    Task<JobOffer> UpdateAsync(JobOffer jobOffer);
    Task<JobOffer> UpdateWithDetailsAsync(JobOffer jobOffer);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<Dictionary<JobOfferStatus, int>> GetStatisticsAsync(Guid? userId);
}

public class JobOfferRepository : IJobOfferRepository
{
    private readonly JobOfferDbContext _db;
    private readonly ILogger<JobOfferRepository> _logger;

    public JobOfferRepository(JobOfferDbContext db, ILogger<JobOfferRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<JobOffer?> GetByIdAsync(Guid id)
        => await _db.JobOffers.FindAsync(id);

    // This replaces GetByIdWithHistoryAsync. It loads the 3 child tables.
    public async Task<JobOffer?> GetByIdWithDetailsAsync(Guid id)
        => await _db.JobOffers
            .Include(j => j.Skills)
            .Include(j => j.Responsibilities)
            .Include(j => j.Benefits)
            .FirstOrDefaultAsync(j => j.Id == id);

    public async Task<List<JobOffer>> GetAllAsync(Guid? userId, int page, int pageSize)
    {
        var query = _db.JobOffers.AsQueryable();

        // Filter by the user who ingested the job offer
        if (userId.HasValue)
            query = query.Where(j => j.UserId == userId.Value);

        return await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(Guid? userId)
    {
        var query = _db.JobOffers.AsQueryable();
        if (userId.HasValue)
            query = query.Where(j => j.UserId == userId.Value);
        return await query.CountAsync();
    }

    public async Task<JobOffer> CreateAsync(JobOffer jobOffer)
    {
        _db.JobOffers.Add(jobOffer);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created job offer {Id}", jobOffer.Id);
        return jobOffer;
    }

    public async Task<JobOffer> UpdateAsync(JobOffer jobOffer)
    {
        jobOffer.UpdatedAt = DateTime.UtcNow;
        _db.JobOffers.Update(jobOffer);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated job offer {Id}", jobOffer.Id);
        return jobOffer;
    }
    public async Task<JobOffer> UpdateWithDetailsAsync(JobOffer jobOffer)
    {
        // 1. Always update the modification timestamp
        jobOffer.UpdatedAt = DateTime.UtcNow;
        
        // 2. We do NOT call _db.JobOffers.Update(jobOffer) here!
        // Why? Because in the Service layer, we fetched this jobOffer using GetByIdWithDetailsAsync().
        // That means Entity Framework Core is already "Tracking" this object in memory. 
        // When you modified the lists (.Clear(), .Add()) in the Service layer, EF Core was watching.
        
        // 3. Just save the tracked changes to PostgreSQL
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Successfully updated job offer {Id} along with its skills, responsibilities, and benefits.", jobOffer.Id);
        
        return jobOffer;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var jobOffer = await _db.JobOffers.FindAsync(id);
        if (jobOffer == null) return false;

        _db.JobOffers.Remove(jobOffer);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted job offer {Id}", id);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
        => await _db.JobOffers.AnyAsync(j => j.Id == id);

    public async Task<Dictionary<JobOfferStatus, int>> GetStatisticsAsync(Guid? userId)
    {
        var query = _db.JobOffers.AsQueryable();
        if (userId.HasValue)
            query = query.Where(j => j.UserId == userId.Value);

        return await query
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }
}ParseOptions.0.json�9
6/app/src/job-offer-service/services/JobOfferService.cs�8using JobOfferService.DTOs;
using JobOfferService.Entities;
using JobOfferService.Repositories;

namespace JobOfferService.Services;

public interface IJobOfferService
{
    // Write Operations
    Task<Guid> SubmitRawJobOfferAsync(SubmitJobOfferDto dto);
    Task ProcessExtractedDataAsync(Guid jobId, ExtractedJobDto dto);
    Task UpdateStatusAsync(Guid id, UpdateJobStatusDto dto);
    Task DeleteAsync(Guid id);

    // Read Operations
    Task<JobOfferDetailDto?> GetByIdAsync(Guid id);
    Task<JobOfferListDto> GetAllAsync(Guid userId, int page, int pageSize);
    Task<JobOfferStatisticsDto> GetStatisticsAsync(Guid userId);
}

public class JobOfferService : IJobOfferService
{
    private readonly IJobOfferRepository _repository;
    // private readonly IKafkaPublisher _kafkaPublisher; // (Uncomment when you add Kafka)

    public JobOfferService(IJobOfferRepository repository)
    {
        _repository = repository;
    }

    // 1. STEP ONE: User pastes a link or text
    public async Task<Guid> SubmitRawJobOfferAsync(SubmitJobOfferDto dto)
    {
        var jobOffer = new JobOffer
        {
            UserId = dto.UserId,
            RawDescription = dto.RawText ?? "No description provided",
            SourceUrl = dto.SourceUrl,
            Status = JobOfferStatus.DRAFT, // Starts as a draft while AI works
            
            // Temporary placeholders until the AI extracts the real data
            EnterpriseName = "Pending AI Extraction...",
            JobRole = "Pending AI Extraction..."
        };

        await _repository.CreateAsync(jobOffer);

        // TODO: Publish event to Kafka: "ExtractJobDataEvent(jobOffer.Id)"
        // await _kafkaPublisher.PublishAsync(new ExtractJobDataEvent(jobOffer.Id));

        return jobOffer.Id;
    }

    // 2. STEP TWO: AI finishes and sends the structured JSON here
    public async Task ProcessExtractedDataAsync(Guid jobId, ExtractedJobDto dto)
    {
        // We must fetch it WITH its details to update properly
        var jobOffer = await _repository.GetByIdWithDetailsAsync(jobId);
        if (jobOffer == null) throw new KeyNotFoundException("Job offer not found");

        // Update scalar properties
        jobOffer.EnterpriseName = dto.EnterpriseName;
        jobOffer.EnterpriseDescription = dto.EnterpriseDescription;
        jobOffer.JobRole = dto.JobRole;
        jobOffer.RawDescription = dto.RawDescription;
        jobOffer.RequiredExperienceYears = dto.RequiredExperienceYears;
        jobOffer.SeniorityLevel = dto.SeniorityLevel;
        jobOffer.EmploymentType = dto.EmploymentType;
        jobOffer.Location = dto.Location;
        jobOffer.LocationType = dto.LocationType;
        jobOffer.EducationRequirements = dto.EducationRequirements;
        jobOffer.Status = JobOfferStatus.OPEN; // Now it's ready!

        // Clear existing lists (in case this is a re-extraction)
        jobOffer.Skills.Clear();
        jobOffer.Responsibilities.Clear();
        jobOffer.Benefits.Clear();

        // Map arrays to Database Entities
        foreach (var reqSkill in dto.RequiredSkills)
        {
            jobOffer.Skills.Add(new JobSkill { Name = reqSkill, Type = SkillType.HARD_SKILL, IsMandatory = true });
        }
        foreach (var softSkill in dto.SoftSkills)
        {
            jobOffer.Skills.Add(new JobSkill { Name = softSkill, Type = SkillType.SOFT_SKILL, IsMandatory = false });
        }
        foreach (var resp in dto.Responsibilities)
        {
            jobOffer.Responsibilities.Add(new JobResponsibility { Description = resp });
        }
        foreach (var benefit in dto.Benefits)
        {
            jobOffer.Benefits.Add(new JobBenefit { Description = benefit });
        }

        // Save everything to the database
        await _repository.UpdateWithDetailsAsync(jobOffer);

        // TODO: Publish event: "JobOfferReadyForCvGenerationEvent(jobOffer.Id)"
    }

    // 3. GET FULL DETAILS (Used by the frontend and gRPC)
    public async Task<JobOfferDetailDto?> GetByIdAsync(Guid id)
    {
        var job = await _repository.GetByIdWithDetailsAsync(id);
        if (job == null) return null;

        // Map Entity -> DTO (You can use AutoMapper for this later to save time)
        return new JobOfferDetailDto(
            Id: job.Id,
            UserId: job.UserId,
            EnterpriseName: job.EnterpriseName,
            EnterpriseDescription: job.EnterpriseDescription,
            JobRole: job.JobRole,
            RawDescription: job.RawDescription,
            RequiredExperienceYears: job.RequiredExperienceYears,
            SeniorityLevel: job.SeniorityLevel,
            EmploymentType: job.EmploymentType,
            Location: job.Location,
            LocationType: job.LocationType,
            EducationRequirements: job.EducationRequirements,
            SourceUrl: job.SourceUrl,
            Status: job.Status.ToString(),
            CreatedAt: job.CreatedAt,
            UpdatedAt: job.UpdatedAt,
            Skills: job.Skills.Select(s => new JobSkillDto(s.Id, s.Name, s.Type.ToString(), s.IsMandatory)).ToList(),
            Responsibilities: job.Responsibilities.Select(r => new JobResponsibilityDto(r.Id, r.Description)).ToList(),
            Benefits: job.Benefits.Select(b => new JobBenefitDto(b.Id, b.Description)).ToList()
        );
    }

    // 4. GET ALL (Summary view for the dashboard)
    public async Task<JobOfferListDto> GetAllAsync(Guid userId, int page, int pageSize)
    {
        var total = await _repository.GetTotalCountAsync(userId);
        var jobs = await _repository.GetAllAsync(userId, page, pageSize);

        var items = jobs.Select(job => new JobOfferSummaryDto(
            Id: job.Id,
            UserId: job.UserId,
            EnterpriseName: job.EnterpriseName,
            JobRole: job.JobRole,
            Location: job.Location,
            Status: job.Status.ToString(),
            CreatedAt: job.CreatedAt
        )).ToList();

        return new JobOfferListDto(items, total, page, pageSize);
    }

    public async Task UpdateStatusAsync(Guid id, UpdateJobStatusDto dto)
    {
        var job = await _repository.GetByIdAsync(id);
        if (job == null) throw new KeyNotFoundException("Job not found");

        if (Enum.TryParse<JobOfferStatus>(dto.Status, true, out var status))
        {
            job.Status = status;
            await _repository.UpdateAsync(job);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<JobOfferStatisticsDto> GetStatisticsAsync(Guid userId)
    {
        var stats = await _repository.GetStatisticsAsync(userId);
        return new JobOfferStatisticsDto(
            Total: stats.Values.Sum(),
            Draft: stats.GetValueOrDefault(JobOfferStatus.DRAFT, 0),
            Open: stats.GetValueOrDefault(JobOfferStatus.OPEN, 0),
            Closed: stats.GetValueOrDefault(JobOfferStatus.CLOSED, 0),
            Archived: stats.GetValueOrDefault(JobOfferStatus.ARCHIVED, 0)
        );
    }
}ParseOptions.0.json�
:/app/src/job-offer-service/validators/JobOfferValidator.cs�using FluentValidation;
using JobOfferService.DTOs;
using JobOfferService.Entities; // Needed for the enum validation

namespace JobOfferService.Validators;

public class SubmitJobOfferValidator : AbstractValidator<SubmitJobOfferDto>
{
    public SubmitJobOfferValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        // Must have either raw text OR a URL to be valid
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.RawText) || !string.IsNullOrWhiteSpace(x.SourceUrl))
            .WithMessage("You must provide either RawText or a SourceUrl.");
    }
}

public class ExtractedJobValidator : AbstractValidator<ExtractedJobDto>
{
    public ExtractedJobValidator()
    {
        RuleFor(x => x.EnterpriseName)
            .NotEmpty().WithMessage("Enterprise name is required")
            .MaximumLength(200).WithMessage("Enterprise name cannot exceed 200 characters");

        RuleFor(x => x.JobRole)
            .NotEmpty().WithMessage("Job role is required")
            .MaximumLength(150).WithMessage("Job role cannot exceed 150 characters");

        RuleFor(x => x.RawDescription)
            .NotEmpty().WithMessage("Raw description cannot be empty");
            
        // Ensure lists aren't null (they can be empty, but not null)
        RuleFor(x => x.RequiredSkills).NotNull();
        RuleFor(x => x.Responsibilities).NotNull();
    }
}

public class UpdateJobStatusValidator : AbstractValidator<UpdateJobStatusDto>
{
    public UpdateJobStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required")
            .Must(BeValidStatus).WithMessage("Invalid status value. Valid values: DRAFT, OPEN, CLOSED, ARCHIVED");
    }

    private static bool BeValidStatus(string status)
    {
        var validStatuses = new[] { "DRAFT", "OPEN", "CLOSED", "ARCHIVED" };
        return validStatuses.Contains(status.ToUpperInvariant());
    }
}ParseOptions.0.json�
P/app/src/job-offer-service/obj/Debug/net10.0/job-offer-service.GlobalUsings.g.cs�// <auto-generated/>
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
ParseOptions.0.json�
\/app/src/job-offer-service/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs�// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
ParseOptions.0.json�
N/app/src/job-offer-service/obj/Debug/net10.0/job-offer-service.AssemblyInfo.cs�//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("job-offer-service")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0")]
[assembly: System.Reflection.AssemblyProductAttribute("job-offer-service")]
[assembly: System.Reflection.AssemblyTitleAttribute("job-offer-service")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json�
a/app/src/job-offer-service/obj/Debug/net10.0/job-offer-service.MvcApplicationPartsAssemblyInfo.cs�//------------------------------------------------------------------------------
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

ParseOptions.0.json�
D/app/src/job-offer-service/obj/Debug/net10.0/EFCoreNpgsqlPgvector.cs�//------------------------------------------------------------------------------
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

ParseOptions.0.json�
�/app/src/job-offer-service/obj/Debug/net10.0/Microsoft.AspNetCore.App.SourceGenerators/Microsoft.AspNetCore.SourceGenerators.PublicProgramSourceGenerator/PublicTopLevelProgram.Generated.g.cs�// <auto-generated />
/// <summary>
/// Auto-generated public partial Program class for top-level statement apps.
/// </summary>
public partial class Program { }ParseOptions.0.json