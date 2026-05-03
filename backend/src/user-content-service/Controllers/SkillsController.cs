using CVGenerator.Shared;
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
}