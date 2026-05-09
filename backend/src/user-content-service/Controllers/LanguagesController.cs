using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.LanguageEvent;
using UserContentService.Services;
using UserContentService.dto.Language;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LanguagesController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly KafkaProducerService _kafkaProducer;

    public LanguagesController(ContentDbContext db, KafkaProducerService kafkaProducer)
    {
        _db = db;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var languages = userId.HasValue
            ? await _db.Languages.Where(l => l.UserId == userId.Value).ToListAsync()
            : await _db.Languages.ToListAsync();

        var response = languages.Select(l => new LanguageResponseDto
        {
            Id = l.Id,
            Name = l.Name,
            Level = l.Level,
            UserId = l.UserId
        }).ToList();

        return Ok(ApiResponse<List<LanguageResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var l = await _db.Languages.FindAsync(id);
        if (l == null) return NotFound(ApiResponse<LanguageResponseDto>.Error("Language not found"));

        var response = new LanguageResponseDto
        {
            Id = l.Id,
            Name = l.Name,
            Level = l.Level,
            UserId = l.UserId
        };
        return Ok(ApiResponse<LanguageResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLanguageDto dto)
    {
        var language = new Language { Name = dto.Name, Level = dto.Level, UserId = dto.UserId };
        _db.Languages.Add(language);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("language-created", new LanguageCreatedEvent
        {
            Id = language.Id,
            Name = language.Name,
            Level = language.Level,
            UserId = language.UserId
        });

        var response = new LanguageResponseDto { Id = language.Id, Name = language.Name, Level = language.Level, UserId = language.UserId };
        return CreatedAtAction(nameof(GetById), new { id = language.Id }, ApiResponse<LanguageResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLanguageDto dto)
    {
        var language = await _db.Languages.FindAsync(id);
        if (language == null) return NotFound(ApiResponse<LanguageResponseDto>.Error("Language not found"));

        language.Name = dto.Name;
        language.Level = dto.Level;
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("language-updated", new LanguageUpdatedEvent
        {
            Id = language.Id,
            Name = language.Name,
            Level = language.Level,
            UserId = language.UserId
        });

        var response = new LanguageResponseDto { Id = language.Id, Name = language.Name, Level = language.Level, UserId = language.UserId };
        return Ok(ApiResponse<LanguageResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var language = await _db.Languages.FindAsync(id);
        if (language == null) return NotFound(ApiResponse<object>.Error("Language not found"));

        var userId = language.UserId;

        _db.Languages.Remove(language);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("language-deleted", new LanguageDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        return NoContent();
    }

}
