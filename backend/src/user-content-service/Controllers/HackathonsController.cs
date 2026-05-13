using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.HackathonEvent;
using UserContentService.Services;
using UserContentService.dto.Hackathon;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HackathonsController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly KafkaProducerService _kafkaProducer;

    public HackathonsController(ContentDbContext db, KafkaProducerService kafkaProducer)
    {
        _db = db;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var hackathons = userId.HasValue
            ? await _db.Hackathons.Where(h => h.UserId == userId.Value).ToListAsync()
            : await _db.Hackathons.ToListAsync();

        var response = hackathons.Select(h => new HackathonResponseDto
        {
            Id = h.Id,
            Name = h.Name,
            Organization = h.Organization,
            Date = h.Date,
            Description = h.Description,
            Role = h.Role,
            Result = h.Result,
            UserId = h.UserId
        }).ToList();

        return Ok(ApiResponse<List<HackathonResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var h = await _db.Hackathons.FindAsync(id);
        if (h == null) return NotFound(ApiResponse<HackathonResponseDto>.Error("Hackathon not found"));

        var response = new HackathonResponseDto
        {
            Id = h.Id,
            Name = h.Name,
            Organization = h.Organization,
            Date = h.Date,
            Description = h.Description,
            Role = h.Role,
            Result = h.Result,
            UserId = h.UserId
        };
        return Ok(ApiResponse<HackathonResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHackathonDto dto)
    {
        var h = new Hackathon
        {
            Name = dto.Name,
            Organization = dto.Organization,
            Date = dto.Date,
            Description = dto.Description,
            Role = dto.Role,
            Result = dto.Result,
            UserId = dto.UserId
        };

        _db.Hackathons.Add(h);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("hackathon-created", new HackathonCreatedEvent
        {
            Id = h.Id,
            Name = h.Name,
            Organization = h.Organization,
            Date = h.Date,
            Description = h.Description,
            Role = h.Role,
            Result = h.Result,
            UserId = h.UserId
        });

        var response = new HackathonResponseDto
        {
            Id = h.Id,
            Name = h.Name,
            Organization = h.Organization,
            Date = h.Date,
            Description = h.Description,
            Role = h.Role,
            Result = h.Result,
            UserId = h.UserId
        };
        return CreatedAtAction(nameof(GetById), new { id = h.Id }, ApiResponse<HackathonResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHackathonDto dto)
    {
        var h = await _db.Hackathons.FindAsync(id);
        if (h == null) return NotFound(ApiResponse<HackathonResponseDto>.Error("Hackathon not found"));

        h.Name = dto.Name;
        h.Organization = dto.Organization;
        h.Date = dto.Date;
        h.Description = dto.Description;
        h.Role = dto.Role;
        h.Result = dto.Result;

        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("hackathon-updated", new HackathonUpdatedEvent
        {
            Id = h.Id,
            Name = h.Name,
            Organization = h.Organization,
            Date = h.Date,
            Description = h.Description,
            Role = h.Role,
            Result = h.Result,
            UserId = h.UserId
        });

        var response = new HackathonResponseDto
        {
            Id = h.Id,
            Name = h.Name,
            Organization = h.Organization,
            Date = h.Date,
            Description = h.Description,
            Role = h.Role,
            Result = h.Result,
            UserId = h.UserId
        };
        return Ok(ApiResponse<HackathonResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var h = await _db.Hackathons.FindAsync(id);
        if (h == null) return NotFound(ApiResponse<object>.Error("Hackathon not found"));

        var userId = h.UserId;

        _db.Hackathons.Remove(h);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("hackathon-deleted", new HackathonDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        return NoContent();
    }

}
