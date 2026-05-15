using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.CVProfileEvent;
using UserContentService.Services;
using UserContentService.dto.CVProfile;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/cvprofiles")]
public class CVProfilesController : ApiControllerBase
{
    private readonly ContentDbContext _db;
    private readonly KafkaProducerService _kafkaProducer;

    public CVProfilesController(ContentDbContext db, KafkaProducerService kafkaProducer)
    {
        _db = db;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var profiles = userId.HasValue
            ? await _db.CVProfiles.Where(p => p.UserId == userId.Value).ToListAsync()
            : await _db.CVProfiles.ToListAsync();

        var response = profiles.Select(p => new CVProfileResponseDto
        {
            Id = p.Id,
            Title = p.Title,
            Summary = p.Summary,
            UserId = p.UserId
        }).ToList();

        return Ok(ApiResponse<List<CVProfileResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var p = await _db.CVProfiles.FindAsync(id);
        if (p == null) return NotFound(ApiResponse<CVProfileResponseDto>.Error("CV Profile not found"));

        var response = new CVProfileResponseDto
        {
            Id = p.Id,
            Title = p.Title,
            Summary = p.Summary,
            UserId = p.UserId
        };
        return Ok(ApiResponse<CVProfileResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCVProfileDto dto)
    {
        var profile = new CVProfile { Title = dto.Title, Summary = dto.Summary, UserId = RequiredUserId };
        
        _db.CVProfiles.Add(profile);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("cvprofile-created", new CVProfileCreatedEvent
        {
            Id = profile.Id,
            Title = profile.Title,
            Summary = profile.Summary,
            UserId = profile.UserId
        });

        var response = new CVProfileResponseDto { Id = profile.Id, Title = profile.Title, Summary = profile.Summary, UserId = profile.UserId };
        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, ApiResponse<CVProfileResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCVProfileDto dto)
    {
        var profile = await _db.CVProfiles.FindAsync(id);
        if (profile == null) return NotFound(ApiResponse<CVProfileResponseDto>.Error("CV Profile not found"));

        profile.Title = dto.Title;
        profile.Summary = dto.Summary;

        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("cvprofile-updated", new CVProfileUpdatedEvent
        {
            Id = profile.Id,
            Title = profile.Title,
            Summary = profile.Summary,
            UserId = profile.UserId
        });

        var response = new CVProfileResponseDto { Id = profile.Id, Title = profile.Title, Summary = profile.Summary, UserId = profile.UserId };
        return Ok(ApiResponse<CVProfileResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var profile = await _db.CVProfiles.FindAsync(id);
        if (profile == null) return NotFound(ApiResponse<object>.Error("CV Profile not found"));

        var userId = profile.UserId;

        _db.CVProfiles.Remove(profile);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("cvprofile-deleted", new CVProfileDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        return NoContent();
    }

}
