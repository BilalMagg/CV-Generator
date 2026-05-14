using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService.Entity;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(WorkflowDbContext db, ILogger<ProjectsController> logger)
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
}
