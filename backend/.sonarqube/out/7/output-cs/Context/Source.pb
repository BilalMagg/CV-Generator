¥
1/app/src/user-content-service/ContentDbContext.csÈusing Microsoft.EntityFrameworkCore;
using Pgvector;
using UserContentService.Entity;

namespace UserContentService;

public class ContentDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<Experience> Experiences { get; set; }

    public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Project>()
            .Property(p => p.DescriptionEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Skill>()
            .Property(s => s.NameEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Experience>()
            .Property(e => e.DescriptionEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Project>()
            .HasOne(p => p.User)
            .WithMany(u => u.Projects)
            .HasForeignKey(p => p.UserId);

        modelBuilder.Entity<Skill>()
            .HasOne(s => s.User)
            .WithMany(u => u.Skills)
            .HasForeignKey(s => s.UserId);

        modelBuilder.Entity<Experience>()
            .HasOne(e => e.User)
            .WithMany(u => u.Experiences)
            .HasForeignKey(e => e.UserId);
    }
}ParseOptions.0.json…
B/app/src/user-content-service/Controllers/ExperiencesController.csÌusing CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService;
using UserContentService.Entity;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExperiencesController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly ILogger<ExperiencesController> _logger;

    public ExperiencesController(ContentDbContext db, ILogger<ExperiencesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var experiences = userId.HasValue
            ? await _db.Experiences.Where(e => e.UserId == userId.Value).ToListAsync()
            : await _db.Experiences.ToListAsync();
        return Ok(ApiResponse<List<Experience>>.Ok(experiences));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var exp = await _db.Experiences.FindAsync(id);
        if (exp == null) return NotFound(ApiResponse<Experience>.Error("Experience not found"));
        return Ok(ApiResponse<Experience>.Ok(exp));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExperienceDto dto)
    {
        var exp = new Experience
        {
            Title = dto.Title,
            Company = dto.Company,
            Description = dto.Description,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ReferenceUrl = dto.ReferenceUrl,
            Status = dto.Status,
            UserId = dto.UserId
        };

        _db.Experiences.Add(exp);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created experience {Id}", exp.Id);
        return Created($"/api/experiences/{exp.Id}", ApiResponse<Experience>.Created(exp));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExperienceDto dto)
    {
        var exp = await _db.Experiences.FindAsync(id);
        if (exp == null) return NotFound(ApiResponse<Experience>.Error("Experience not found"));

        exp.Title = dto.Title;
        exp.Company = dto.Company;
        exp.Description = dto.Description;
        exp.StartDate = dto.StartDate;
        exp.EndDate = dto.EndDate;
        exp.ReferenceUrl = dto.ReferenceUrl;
        exp.Status = dto.Status;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Experience>.Ok(exp));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var exp = await _db.Experiences.FindAsync(id);
        if (exp == null) return NotFound(ApiResponse<object>.Error("Experience not found"));

        _db.Experiences.Remove(exp);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateExperienceDto(
        string Title,
        string? Company,
        string? Description,
        DateTime StartDate,
        DateTime? EndDate,
        string? ReferenceUrl,
        string Status,
        Guid UserId
    );

    public record UpdateExperienceDto(
        string Title,
        string? Company,
        string? Description,
        DateTime StartDate,
        DateTime? EndDate,
        string? ReferenceUrl,
        string Status
    );
}ParseOptions.0.jsonï
?/app/src/user-content-service/Controllers/ProjectsController.csºusing System.ComponentModel.DataAnnotations;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService;
using UserContentService.Entity;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(ContentDbContext db, ILogger<ProjectsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var projects = userId.HasValue
            ? await _db.Projects.Where(p => p.UserId == userId.Value).ToListAsync()
            : await _db.Projects.ToListAsync();
        return Ok(ApiResponse<List<Project>>.Ok(projects));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResponse<Project>.Error("Project not found"));
        return Ok(ApiResponse<Project>.Ok(project));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
    {
        var project = new Project
        {
            Title = dto.Title,
            Description = dto.Description,
            Role = dto.Role,
            Achievements = dto.Achievements,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            RepositoryUrl = dto.RepositoryUrl,
            DemoUrl = dto.DemoUrl,
            Status = dto.Status,
            UserId = dto.UserId,
            SkillsJson = dto.SkillsJson
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created project {Id}", project.Id);
        return Created($"/api/projects/{project.Id}", ApiResponse<Project>.Created(project));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDto dto)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResponse<Project>.Error("Project not found"));

        project.Title = dto.Title;
        project.Description = dto.Description;
        project.Role = dto.Role;
        project.Achievements = dto.Achievements;
        project.StartDate = dto.StartDate;
        project.EndDate = dto.EndDate;
        project.RepositoryUrl = dto.RepositoryUrl;
        project.DemoUrl = dto.DemoUrl;
        project.Status = dto.Status;
        project.SkillsJson = dto.SkillsJson;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Project>.Ok(project));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResponse<object>.Error("Project not found"));

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateProjectDto(
        string Title,
        string? Description,
        string? Role,
        string? Achievements,
        DateTime StartDate,
        DateTime? EndDate,
        string? RepositoryUrl,
        string? DemoUrl,
        string Status,
        Guid UserId,
        string? SkillsJson
    );

    public record UpdateProjectDto(
        string Title,
        string? Description,
        string? Role,
        string? Achievements,
        DateTime StartDate,
        DateTime? EndDate,
        string? RepositoryUrl,
        string? DemoUrl,
        string Status,
        string? SkillsJson
    );
}ParseOptions.0.jsonÏ
=/app/src/user-content-service/Controllers/SkillsController.csïusing CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService;
using UserContentService.Entity;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkillsController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly ILogger<SkillsController> _logger;

    public SkillsController(ContentDbContext db, ILogger<SkillsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var skills = userId.HasValue
            ? await _db.Skills.Where(s => s.UserId == userId.Value).ToListAsync()
            : await _db.Skills.ToListAsync();
        return Ok(ApiResponse<List<Skill>>.Ok(skills));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<Skill>.Error("Skill not found"));
        return Ok(ApiResponse<Skill>.Ok(skill));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSkillDto dto)
    {
        var skill = new Skill
        {
            Name = dto.Name,
            Level = dto.Level,
            YearsOfExperience = dto.YearsOfExperience,
            UserId = dto.UserId,
            Category = dto.Category
        };

        _db.Skills.Add(skill);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created skill {Id}", skill.Id);
        return Created($"/api/skills/{skill.Id}", ApiResponse<Skill>.Created(skill));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSkillDto dto)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<Skill>.Error("Skill not found"));

        skill.Name = dto.Name;
        skill.Level = dto.Level;
        skill.YearsOfExperience = dto.YearsOfExperience;
        skill.Category = dto.Category;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Skill>.Ok(skill));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<object>.Error("Skill not found"));

        _db.Skills.Remove(skill);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateSkillDto(string Name, string? Level, int? YearsOfExperience, Guid UserId, string? Category);
    public record UpdateSkillDto(string Name, string? Level, int? YearsOfExperience, string? Category);
}ParseOptions.0.json‚
2/app/src/user-content-service/Entity/Experience.csñusing System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace UserContentService.Entity;

[Table("experiences")]
public class Experience
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public required string Title { get; set; }

    [MaxLength(150)]
    public string? Company { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? ReferenceUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Ongoing";

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public string? AiSummaryJson { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? DescriptionEmbedding { get; set; }
}ParseOptions.0.jsoné

//app/src/user-content-service/Entity/Project.cs≈	using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace UserContentService.Entity;

[Table("projects")]
public class Project
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public required string Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Role { get; set; }

    [MaxLength(1000)]
    public string? Achievements { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? RepositoryUrl { get; set; }

    [MaxLength(300)]
    public string? DemoUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Ongoing";

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public string? SkillsJson { get; set; }
    public string? AiSummaryJson { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? DescriptionEmbedding { get; set; }
}ParseOptions.0.jsonµ
-/app/src/user-content-service/Entity/Skill.csÓusing System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace UserContentService.Entity;

[Table("skills")]
public class Skill
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(20)]
    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? NameEmbedding { get; set; }
}ParseOptions.0.jsonˇ
,/app/src/user-content-service/Entity/User.csπusing System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserContentService.Entity;

[Table("users")]
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

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<Skill> Skills { get; set; } = new List<Skill>();
    public ICollection<Experience> Experiences { get; set; } = new List<Experience>();
}ParseOptions.0.json›P
8/app/src/user-content-service/Grpc/ContentServiceImpl.csãPusing CommonProtos.Content;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using UserContentService;
using UserContentService.Entity;

namespace UserContentService.Grpc;

public class ContentServiceImpl : CommonProtos.Content.ContentServiceGrpc.ContentServiceGrpcBase
{
    private readonly ContentDbContext _db;
    private readonly ILogger<ContentServiceImpl> _logger;

    public ContentServiceImpl(ContentDbContext db, ILogger<ContentServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Projects
    public override async Task<ProjectProto> GetProjectById(GetProjectByIdRequest request, ServerCallContext context)
    {
        var project = await _db.Projects.FindAsync(Guid.Parse(request.Id));
        if (project == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Project not found"));

        return ToProjectProto(project);
    }

    public override async Task GetProjectsByUserId(GetProjectsByUserIdRequest request, IServerStreamWriter<ProjectProto> responseStream, ServerCallContext context)
    {
        var projects = await _db.Projects.Where(p => p.UserId == Guid.Parse(request.UserId)).ToListAsync();
        foreach (var project in projects)
            await responseStream.WriteAsync(ToProjectProto(project));
    }

    public override async Task<ProjectProto> CreateProject(CreateProjectRequest request, ServerCallContext context)
    {
        var project = new Project
        {
            Title = request.Title,
            Description = request.Description,
            Role = request.Role,
            Achievements = request.Achievements,
            StartDate = DateTime.Parse(request.StartDate),
            EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate),
            RepositoryUrl = request.RepositoryUrl,
            DemoUrl = request.DemoUrl,
            Status = request.Status,
            UserId = Guid.Parse(request.UserId),
            SkillsJson = request.SkillsJson
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created project {Id}", project.Id);
        return ToProjectProto(project);
    }

    public override async Task<ProjectProto> UpdateProject(UpdateProjectRequest request, ServerCallContext context)
    {
        var project = await _db.Projects.FindAsync(Guid.Parse(request.Id));
        if (project == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Project not found"));

        project.Title = request.Title;
        project.Description = request.Description;
        project.Role = request.Role;
        project.Achievements = request.Achievements;
        project.StartDate = DateTime.Parse(request.StartDate);
        project.EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate);
        project.RepositoryUrl = request.RepositoryUrl;
        project.DemoUrl = request.DemoUrl;
        project.Status = request.Status;
        project.SkillsJson = request.SkillsJson;

        await _db.SaveChangesAsync();
        return ToProjectProto(project);
    }

    public override async Task<DeleteProjectResponse> DeleteProject(DeleteProjectRequest request, ServerCallContext context)
    {
        var project = await _db.Projects.FindAsync(Guid.Parse(request.Id));
        if (project == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Project not found"));

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return new DeleteProjectResponse { Success = true };
    }

    // Skills
    public override async Task<SkillProto> GetSkillById(GetSkillByIdRequest request, ServerCallContext context)
    {
        var skill = await _db.Skills.FindAsync(Guid.Parse(request.Id));
        if (skill == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Skill not found"));

        return ToSkillProto(skill);
    }

    public override async Task GetSkillsByUserId(GetSkillsByUserIdRequest request, IServerStreamWriter<SkillProto> responseStream, ServerCallContext context)
    {
        var skills = await _db.Skills.Where(s => s.UserId == Guid.Parse(request.UserId)).ToListAsync();
        foreach (var skill in skills)
            await responseStream.WriteAsync(ToSkillProto(skill));
    }

    public override async Task<SkillProto> CreateSkill(CreateSkillRequest request, ServerCallContext context)
    {
        var skill = new Skill
        {
            Name = request.Name,
            Level = request.Level,
            YearsOfExperience = request.YearsOfExperience,
            UserId = Guid.Parse(request.UserId),
            Category = request.Category
        };

        _db.Skills.Add(skill);
        await _db.SaveChangesAsync();

        return ToSkillProto(skill);
    }

    public override async Task<SkillProto> UpdateSkill(UpdateSkillRequest request, ServerCallContext context)
    {
        var skill = await _db.Skills.FindAsync(Guid.Parse(request.Id));
        if (skill == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Skill not found"));

        skill.Name = request.Name;
        skill.Level = request.Level;
        skill.YearsOfExperience = request.YearsOfExperience;
        skill.Category = request.Category;

        await _db.SaveChangesAsync();
        return ToSkillProto(skill);
    }

    public override async Task<DeleteSkillResponse> DeleteSkill(DeleteSkillRequest request, ServerCallContext context)
    {
        var skill = await _db.Skills.FindAsync(Guid.Parse(request.Id));
        if (skill == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Skill not found"));

        _db.Skills.Remove(skill);
        await _db.SaveChangesAsync();
        return new DeleteSkillResponse { Success = true };
    }

    // Experiences
    public override async Task<ExperienceProto> GetExperienceById(GetExperienceByIdRequest request, ServerCallContext context)
    {
        var exp = await _db.Experiences.FindAsync(Guid.Parse(request.Id));
        if (exp == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Experience not found"));

        return ToExperienceProto(exp);
    }

    public override async Task GetExperiencesByUserId(GetExperiencesByUserIdRequest request, IServerStreamWriter<ExperienceProto> responseStream, ServerCallContext context)
    {
        var exps = await _db.Experiences.Where(e => e.UserId == Guid.Parse(request.UserId)).ToListAsync();
        foreach (var exp in exps)
            await responseStream.WriteAsync(ToExperienceProto(exp));
    }

    public override async Task<ExperienceProto> CreateExperience(CreateExperienceRequest request, ServerCallContext context)
    {
        var exp = new Experience
        {
            Title = request.Title,
            Company = request.Company,
            Description = request.Description,
            StartDate = DateTime.Parse(request.StartDate),
            EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate),
            ReferenceUrl = request.ReferenceUrl,
            Status = request.Status,
            UserId = Guid.Parse(request.UserId)
        };

        _db.Experiences.Add(exp);
        await _db.SaveChangesAsync();

        return ToExperienceProto(exp);
    }

    public override async Task<ExperienceProto> UpdateExperience(UpdateExperienceRequest request, ServerCallContext context)
    {
        var exp = await _db.Experiences.FindAsync(Guid.Parse(request.Id));
        if (exp == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Experience not found"));

        exp.Title = request.Title;
        exp.Company = request.Company;
        exp.Description = request.Description;
        exp.StartDate = DateTime.Parse(request.StartDate);
        exp.EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate);
        exp.ReferenceUrl = request.ReferenceUrl;
        exp.Status = request.Status;

        await _db.SaveChangesAsync();
        return ToExperienceProto(exp);
    }

    public override async Task<DeleteExperienceResponse> DeleteExperience(DeleteExperienceRequest request, ServerCallContext context)
    {
        var exp = await _db.Experiences.FindAsync(Guid.Parse(request.Id));
        if (exp == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Experience not found"));

        _db.Experiences.Remove(exp);
        await _db.SaveChangesAsync();
        return new DeleteExperienceResponse { Success = true };
    }

    private static ProjectProto ToProjectProto(Project p) => new()
    {
        Id = p.Id.ToString(),
        Title = p.Title,
        Description = p.Description ?? "",
        Role = p.Role ?? "",
        Achievements = p.Achievements ?? "",
        StartDate = p.StartDate.ToString("O"),
        EndDate = p.EndDate?.ToString("O") ?? "",
        RepositoryUrl = p.RepositoryUrl ?? "",
        DemoUrl = p.DemoUrl ?? "",
        Status = p.Status,
        UserId = p.UserId.ToString(),
        SkillsJson = p.SkillsJson ?? "",
        AiSummaryJson = p.AiSummaryJson ?? ""
    };

    private static SkillProto ToSkillProto(Skill s) => new()
    {
        Id = s.Id.ToString(),
        Name = s.Name,
        Level = s.Level ?? "",
        YearsOfExperience = s.YearsOfExperience ?? 0,
        UserId = s.UserId.ToString(),
        Category = s.Category ?? ""
    };

    private static ExperienceProto ToExperienceProto(Experience e) => new()
    {
        Id = e.Id.ToString(),
        Title = e.Title,
        Company = e.Company ?? "",
        Description = e.Description ?? "",
        StartDate = e.StartDate.ToString("O"),
        EndDate = e.EndDate?.ToString("O") ?? "",
        ReferenceUrl = e.ReferenceUrl ?? "",
        Status = e.Status,
        UserId = e.UserId.ToString(),
        AiSummaryJson = e.AiSummaryJson ?? ""
    };
}ParseOptions.0.json»
(/app/src/user-content-service/Program.csÜusing Microsoft.EntityFrameworkCore;
using Npgsql;
using UserContentService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ContentDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector();
    var dataSource = dataSourceBuilder.Build();
    options.UseNpgsql(dataSource, o => o.UseVector());
});

builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddGrpc();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
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
app.UseRouting();
app.MapControllers();
app.MapGrpcService<UserContentService.Grpc.ContentServiceImpl>();

app.Run();ParseOptions.0.jsonﬂ
T/app/src/user-content-service/obj/Debug/net10.0/UserContentService.GlobalUsings.g.csÒ// <auto-generated/>
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
ParseOptions.0.jsonΩ
_/app/src/user-content-service/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.csƒ// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
ParseOptions.0.json±
R/app/src/user-content-service/obj/Debug/net10.0/UserContentService.AssemblyInfo.cs≈//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("UserContentService")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0")]
[assembly: System.Reflection.AssemblyProductAttribute("UserContentService")]
[assembly: System.Reflection.AssemblyTitleAttribute("UserContentService")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json∏
e/app/src/user-content-service/obj/Debug/net10.0/UserContentService.MvcApplicationPartsAssemblyInfo.csπ//------------------------------------------------------------------------------
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

ParseOptions.0.jsoná
G/app/src/user-content-service/obj/Debug/net10.0/EFCoreNpgsqlPgvector.cs¶//------------------------------------------------------------------------------
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

ParseOptions.0.jsonÅ
¡/app/src/user-content-service/obj/Debug/net10.0/Microsoft.AspNetCore.App.SourceGenerators/Microsoft.AspNetCore.SourceGenerators.PublicProgramSourceGenerator/PublicTopLevelProgram.Generated.g.cs•// <auto-generated />
/// <summary>
/// Auto-generated public partial Program class for top-level statement apps.
/// </summary>
public partial class Program { }ParseOptions.0.json