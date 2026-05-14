using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.ExperienceEvent;
using UserContentService.Services;
using UserContentService.dto.Experience;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExperiencesController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly ILogger<ExperiencesController> _logger;
    private readonly KafkaProducerService _kafkaProducer;

    public ExperiencesController(ContentDbContext db, ILogger<ExperiencesController> logger, KafkaProducerService kafkaProducer )
    {
        _db = db;
        _logger = logger;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var experiences = userId.HasValue
            ? await _db.Experiences.Where(e => e.UserId == userId.Value).ToListAsync()
            : await _db.Experiences.ToListAsync();

        var response = experiences.Select(e => new ExperienceResponseDto
        {
            Id = e.Id,
            Title = e.Title,
            Company = e.Company,
            Description = e.Description,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            ReferenceUrl = e.ReferenceUrl,
            Status = e.Status,
            UserId = e.UserId
        }).ToList();

        return Ok(ApiResponse<List<ExperienceResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var exp = await _db.Experiences.FindAsync(id);
        if (exp == null) return NotFound(ApiResponse<ExperienceResponseDto>.Error("Experience not found"));

        var response = new ExperienceResponseDto
        {
            Id = exp.Id,
            Title = exp.Title,
            Company = exp.Company,
            Description = exp.Description,
            StartDate = exp.StartDate,
            EndDate = exp.EndDate,
            ReferenceUrl = exp.ReferenceUrl,
            Status = exp.Status,
            UserId = exp.UserId
        };

        return Ok(ApiResponse<ExperienceResponseDto>.Ok(response));
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

        await _kafkaProducer.PublishAsync("experience-created", new ExperienceCreatedEvent
        {
            Id = exp.Id,
            Title = exp.Title,
            Company = exp.Company,
            Description = exp.Description,
            StartDate = exp.StartDate,
            EndDate = exp.EndDate,
            ReferenceUrl = exp.ReferenceUrl,
            Status = exp.Status,
            UserId = exp.UserId
        });

        _logger.LogInformation("Created experience {Id} and published event", exp.Id);

        var response = new ExperienceResponseDto
        {
            Id = exp.Id,
            Title = exp.Title,
            Company = exp.Company,
            Description = exp.Description,
            StartDate = exp.StartDate,
            EndDate = exp.EndDate,
            ReferenceUrl = exp.ReferenceUrl,
            Status = exp.Status,
            UserId = exp.UserId
        };

        return Created($"/api/experiences/{exp.Id}", ApiResponse<ExperienceResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExperienceDto dto)
    {
        var exp = await _db.Experiences.FindAsync(id);
        if (exp == null) return NotFound(ApiResponse<ExperienceResponseDto>.Error("Experience not found"));

        exp.Title = dto.Title;
        exp.Company = dto.Company;
        exp.Description = dto.Description;
        exp.StartDate = dto.StartDate;
        exp.EndDate = dto.EndDate;
        exp.ReferenceUrl = dto.ReferenceUrl;
        exp.Status = dto.Status;

        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("experience-updated", new ExperienceUpdatedEvent
        {
            Id = exp.Id,
            Title = exp.Title,
            Company = exp.Company,
            Description = exp.Description,
            StartDate = exp.StartDate,
            EndDate = exp.EndDate,
            ReferenceUrl = exp.ReferenceUrl,
            Status = exp.Status,
            UserId = exp.UserId
        });

        var response = new ExperienceResponseDto
        {
            Id = exp.Id,
            Title = exp.Title,
            Company = exp.Company,
            Description = exp.Description,
            StartDate = exp.StartDate,
            EndDate = exp.EndDate,
            ReferenceUrl = exp.ReferenceUrl,
            Status = exp.Status,
            UserId = exp.UserId
        };

        return Ok(ApiResponse<ExperienceResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var exp = await _db.Experiences.FindAsync(id);
        if (exp == null) return NotFound(ApiResponse<object>.Error("Experience not found"));

        var userId = exp.UserId;

        _db.Experiences.Remove(exp);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("experience-deleted", new ExperienceDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        _logger.LogInformation("Deleted experience {Id} and published event", id);
        return NoContent();
    }

}