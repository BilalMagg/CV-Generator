using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.ProjectEvent;
using UserContentService.Services;
using UserContentService.dto.Project;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ApiControllerBase
{
    private readonly ContentDbContext _db;
    private readonly ILogger<ProjectsController> _logger;
    private readonly KafkaProducerService _kafkaProducer;

    public ProjectsController(ContentDbContext db, ILogger<ProjectsController> logger, KafkaProducerService kafkaProducer )
    {
        _db = db;
        _logger = logger;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        userId ??= CurrentUserId;

        var projects = userId.HasValue
            ? await _db.Projects.Where(p => p.UserId == userId.Value).ToListAsync()
            : await _db.Projects.ToListAsync();

        var response = projects.Select(p => new ProjectResponseDto
        {
            Id = p.Id,
            Title = p.Title,
            Description = p.Description,
            Role = p.Role,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            RepositoryUrl = p.RepositoryUrl,
            DemoUrl = p.DemoUrl,
            Status = p.Status,
            UserId = p.UserId,
            SkillsJson = p.SkillsJson
        }).ToList();

        return Ok(ApiResponse<List<ProjectResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var p = await _db.Projects.FindAsync(id);
        if (p == null) return NotFound(ApiResponse<ProjectResponseDto>.Error("Project not found"));

        var response = new ProjectResponseDto
        {
            Id = p.Id,
            Title = p.Title,
            Description = p.Description,
            Role = p.Role,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            RepositoryUrl = p.RepositoryUrl,
            DemoUrl = p.DemoUrl,
            Status = p.Status,
            UserId = p.UserId,
            SkillsJson = p.SkillsJson
        };

        return Ok(ApiResponse<ProjectResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
    {
        var project = new Project
        {
            Title = dto.Title,
            Description = dto.Description,
            Role = dto.Role,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            RepositoryUrl = dto.RepositoryUrl,
            DemoUrl = dto.DemoUrl,
            Status = dto.Status,
            UserId = RequiredUserId,
            SkillsJson = dto.SkillsJson
        };
        
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("project-created", new ProjectCreatedEvent
        {
            Id = project.Id,
            Title = project.Title,
            Description = project.Description,
            Role = project.Role,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            RepositoryUrl = project.RepositoryUrl,
            DemoUrl = project.DemoUrl,
            Status = project.Status,
            SkillsJson = project.SkillsJson,
            UserId = project.UserId
        });

        _logger.LogInformation("Created project {Id} and published event", project.Id);

        var response = new ProjectResponseDto
        {
            Id = project.Id,
            Title = project.Title,
            Description = project.Description,
            Role = project.Role,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            RepositoryUrl = project.RepositoryUrl,
            DemoUrl = project.DemoUrl,
            Status = project.Status,
            UserId = project.UserId,
            SkillsJson = project.SkillsJson
        };

        return Created($"/api/projects/{project.Id}", ApiResponse<ProjectResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDto dto)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResponse<ProjectResponseDto>.Error("Project not found"));

        project.Title = dto.Title;
        project.Description = dto.Description;
        project.Role = dto.Role;
        project.StartDate = dto.StartDate;
        project.EndDate = dto.EndDate;
        project.RepositoryUrl = dto.RepositoryUrl;
        project.DemoUrl = dto.DemoUrl;
        project.Status = dto.Status;
        project.SkillsJson = dto.SkillsJson;

        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("project-updated", new ProjectUpdatedEvent
        {
            Id = project.Id,
            Title = project.Title,
            Description = project.Description,
            Role = project.Role,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            RepositoryUrl = project.RepositoryUrl,
            DemoUrl = project.DemoUrl,
            Status = project.Status,
            SkillsJson = project.SkillsJson,
            UserId = project.UserId
        });

        var response = new ProjectResponseDto
        {
            Id = project.Id,
            Title = project.Title,
            Description = project.Description,
            Role = project.Role,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            RepositoryUrl = project.RepositoryUrl,
            DemoUrl = project.DemoUrl,
            Status = project.Status,
            SkillsJson = project.SkillsJson,
            UserId = project.UserId
        };

        return Ok(ApiResponse<ProjectResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResponse<object>.Error("Project not found"));

        var userId = project.UserId;

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("project-deleted", new ProjectDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        _logger.LogInformation("Deleted project {Id} and published event", id);
        return NoContent();
    }

}