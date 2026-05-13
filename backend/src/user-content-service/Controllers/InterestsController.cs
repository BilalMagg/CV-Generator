using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.InterestEvent;
using UserContentService.Services;
using UserContentService.dto.Interest;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InterestsController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly KafkaProducerService _kafkaProducer;

    public InterestsController(ContentDbContext db, KafkaProducerService kafkaProducer)
    {
        _db = db;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var interests = userId.HasValue
            ? await _db.Interests.Where(i => i.UserId == userId.Value).ToListAsync()
            : await _db.Interests.ToListAsync();

        var response = interests.Select(i => new InterestResponseDto
        {
            Id = i.Id,
            Name = i.Name,
            UserId = i.UserId
        }).ToList();

        return Ok(ApiResponse<List<InterestResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var interest = await _db.Interests.FindAsync(id);
        if (interest == null) return NotFound(ApiResponse<InterestResponseDto>.Error("Interest not found"));

        var response = new InterestResponseDto { Id = interest.Id, Name = interest.Name, UserId = interest.UserId };
        return Ok(ApiResponse<InterestResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInterestDto dto)
    {
        var interest = new Interest { Name = dto.Name, UserId = dto.UserId };
        _db.Interests.Add(interest);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("interest-created", new InterestCreatedEvent
        {
            Id = interest.Id,
            Name = interest.Name,
            UserId = interest.UserId
        });

        var response = new InterestResponseDto { Id = interest.Id, Name = interest.Name, UserId = interest.UserId };
        return CreatedAtAction(nameof(GetById), new { id = interest.Id }, ApiResponse<InterestResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInterestDto dto)
    {
        var interest = await _db.Interests.FindAsync(id);
        if (interest == null) return NotFound(ApiResponse<InterestResponseDto>.Error("Interest not found"));

        interest.Name = dto.Name;
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("interest-updated", new InterestUpdatedEvent
        {
            Id = interest.Id,
            Name = interest.Name,
            UserId = interest.UserId
        });

        var response = new InterestResponseDto { Id = interest.Id, Name = interest.Name, UserId = interest.UserId };
        return Ok(ApiResponse<InterestResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var interest = await _db.Interests.FindAsync(id);
        if (interest == null) return NotFound(ApiResponse<object>.Error("Interest not found"));

        var userId = interest.UserId;

        _db.Interests.Remove(interest);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("interest-deleted", new InterestDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        return NoContent();
    }

}
