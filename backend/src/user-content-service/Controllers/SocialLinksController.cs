using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.SocialLinkEvent;
using UserContentService.Services;
using UserContentService.dto.SocialLink;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SocialLinksController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly KafkaProducerService _kafkaProducer;

    public SocialLinksController(ContentDbContext db, KafkaProducerService kafkaProducer)
    {
        _db = db;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var links = userId.HasValue
            ? await _db.SocialLinks.Where(s => s.UserId == userId.Value).ToListAsync()
            : await _db.SocialLinks.ToListAsync();

        var response = links.Select(s => new SocialLinkResponseDto
        {
            Id = s.Id,
            Platform = s.Platform,
            Url = s.Url,
            UserId = s.UserId
        }).ToList();

        return Ok(ApiResponse<List<SocialLinkResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var link = await _db.SocialLinks.FindAsync(id);
        if (link == null) return NotFound(ApiResponse<SocialLinkResponseDto>.Error("Social link not found"));

        var response = new SocialLinkResponseDto
        {
            Id = link.Id,
            Platform = link.Platform,
            Url = link.Url,
            UserId = link.UserId
        };
        return Ok(ApiResponse<SocialLinkResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSocialLinkDto dto)
    {
        var link = new SocialLink { Platform = dto.Platform, Url = dto.Url, UserId = dto.UserId };
        _db.SocialLinks.Add(link);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("social-link-created", new SocialLinkCreatedEvent
        {
            Id = link.Id,
            Platform = link.Platform,
            Url = link.Url,
            UserId = link.UserId
        });

        var response = new SocialLinkResponseDto { Id = link.Id, Platform = link.Platform, Url = link.Url, UserId = link.UserId };
        return CreatedAtAction(nameof(GetById), new { id = link.Id }, ApiResponse<SocialLinkResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSocialLinkDto dto)
    {
        var link = await _db.SocialLinks.FindAsync(id);
        if (link == null) return NotFound(ApiResponse<SocialLinkResponseDto>.Error("Social link not found"));

        link.Platform = dto.Platform;
        link.Url = dto.Url;
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("social-link-updated", new SocialLinkUpdatedEvent
        {
            Id = link.Id,
            Platform = link.Platform,
            Url = link.Url,
            UserId = link.UserId
        });

        var response = new SocialLinkResponseDto { Id = link.Id, Platform = link.Platform, Url = link.Url, UserId = link.UserId };
        return Ok(ApiResponse<SocialLinkResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var link = await _db.SocialLinks.FindAsync(id);
        if (link == null) return NotFound(ApiResponse<object>.Error("Social link not found"));

        var userId = link.UserId;

        _db.SocialLinks.Remove(link);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("social-link-deleted", new SocialLinkDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        return NoContent();
    }

}
