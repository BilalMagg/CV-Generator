using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.SkillEvent;
using UserContentService.Services;
using UserContentService.dto.Skill;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkillsController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly ILogger<SkillsController> _logger;
    private readonly KafkaProducerService _kafkaProducer;

    public SkillsController(ContentDbContext db, ILogger<SkillsController> logger, KafkaProducerService kafkaProducer )
    {
        _db = db;
        _logger = logger;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var skills = userId.HasValue
            ? await _db.Skills.Where(s => s.UserId == userId.Value).ToListAsync()
            : await _db.Skills.ToListAsync();

        var response = skills.Select(s => new SkillResponseDto
        {
            Id = s.Id,
            Name = s.Name,
            Level = s.Level,
            YearsOfExperience = s.YearsOfExperience,
            UserId = s.UserId,
            Category = s.Category
        }).ToList();

        return Ok(ApiResponse<List<SkillResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var s = await _db.Skills.FindAsync(id);
        if (s == null) return NotFound(ApiResponse<SkillResponseDto>.Error("Skill not found"));

        var response = new SkillResponseDto
        {
            Id = s.Id,
            Name = s.Name,
            Level = s.Level,
            YearsOfExperience = s.YearsOfExperience,
            UserId = s.UserId,
            Category = s.Category
        };

        return Ok(ApiResponse<SkillResponseDto>.Ok(response));
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

        await _kafkaProducer.PublishAsync("skill-created", new SkillCreatedEvent
        {
            Id = skill.Id,
            Name = skill.Name,
            Level = skill.Level,
            YearsOfExperience = skill.YearsOfExperience,
            UserId = skill.UserId,
            Category = skill.Category
        });

        _logger.LogInformation("Created skill {Id} and published event", skill.Id);

        var response = new SkillResponseDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Level = skill.Level,
            YearsOfExperience = skill.YearsOfExperience,
            UserId = skill.UserId,
            Category = skill.Category
        };

        return Created($"/api/skills/{skill.Id}", ApiResponse<SkillResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSkillDto dto)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<SkillResponseDto>.Error("Skill not found"));

        skill.Name = dto.Name;
        skill.Level = dto.Level;
        skill.YearsOfExperience = dto.YearsOfExperience;
        skill.Category = dto.Category;

        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("skill-updated", new SkillUpdatedEvent
        {
            Id = skill.Id,
            Name = skill.Name,
            Level = skill.Level,
            YearsOfExperience = skill.YearsOfExperience,
            UserId = skill.UserId,
            Category = skill.Category
        });

        var response = new SkillResponseDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Level = skill.Level,
            YearsOfExperience = skill.YearsOfExperience,
            UserId = skill.UserId,
            Category = skill.Category
        };

        return Ok(ApiResponse<SkillResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<object>.Error("Skill not found"));

        var userId = skill.UserId;

        _db.Skills.Remove(skill);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("skill-deleted", new SkillDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        _logger.LogInformation("Deleted skill {Id} and published event", id);
        return NoContent();
    }

}