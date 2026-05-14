using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService.Entity;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExperiencesController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<ExperiencesController> _logger;

    public ExperiencesController(WorkflowDbContext db, ILogger<ExperiencesController> logger)
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
}
