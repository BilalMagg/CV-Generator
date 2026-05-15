using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.AcademicActivityEvent;
using UserContentService.Services;
using UserContentService.dto.AcademicActivity;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AcademicActivitiesController : ApiControllerBase
{
    private readonly ContentDbContext _db;
    private readonly KafkaProducerService _kafkaProducer;

    public AcademicActivitiesController(ContentDbContext db, KafkaProducerService kafkaProducer)
    {
        _db = db;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        userId ??= CurrentUserId;

        var activities = userId.HasValue
            ? await _db.AcademicActivities.Where(a => a.UserId == userId.Value).ToListAsync()
            : await _db.AcademicActivities.ToListAsync();

        var response = activities.Select(a => new AcademicActivityResponseDto
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        }).ToList();

        return Ok(ApiResponse<List<AcademicActivityResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var a = await _db.AcademicActivities.FindAsync(id);
        if (a == null) return NotFound(ApiResponse<AcademicActivityResponseDto>.Error("Academic activity not found"));

        var response = new AcademicActivityResponseDto
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        };
        return Ok(ApiResponse<AcademicActivityResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAcademicActivityDto dto)
    {

        var a = new AcademicActivity
        {
            Title = dto.Title,
            Organization = dto.Organization,
            Description = dto.Description,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            UserId = RequiredUserId
        };

        _db.AcademicActivities.Add(a);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("academic-activity-created", new AcademicActivityCreatedEvent
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        });

        var response = new AcademicActivityResponseDto
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        };
        return CreatedAtAction(nameof(GetById), new { id = a.Id }, ApiResponse<AcademicActivityResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAcademicActivityDto dto)
    {
        var a = await _db.AcademicActivities.FindAsync(id);
        if (a == null) return NotFound(ApiResponse<AcademicActivityResponseDto>.Error("Academic activity not found"));

        a.Title = dto.Title;
        a.Organization = dto.Organization;
        a.Description = dto.Description;
        a.StartDate = dto.StartDate;
        a.EndDate = dto.EndDate;

        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("academic-activity-updated", new AcademicActivityUpdatedEvent
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        });

        var response = new AcademicActivityResponseDto
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        };
        return Ok(ApiResponse<AcademicActivityResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var a = await _db.AcademicActivities.FindAsync(id);
        if (a == null) return NotFound(ApiResponse<object>.Error("Academic activity not found"));

        var userId = a.UserId;

        _db.AcademicActivities.Remove(a);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("academic-activity-deleted", new AcademicActivityDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        return NoContent();
    }

}
