using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[Route("api/direct-ai/saved")]
public class SavedToolContentController : BaseApiController
{
    private const string ValidToolPattern = "post|comment|message|emojify";

    private readonly AppDbContext _db;

    public SavedToolContentController(ICurrentUserService currentUser, AppDbContext db) : base(currentUser)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        var items = await _db.SavedToolContent.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
        return Ok(ApiResponse<List<SavedToolContentDto>>.Ok(items.Select(Map).ToList()));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetUserId();
        var item = await _db.SavedToolContent.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (item is null) return NotFound(ApiResponse<SavedToolContentDto>.Error("Saved item not found"));
        return Ok(ApiResponse<SavedToolContentDto>.Ok(Map(item)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSavedToolContentDto dto)
    {
        var userId = GetUserId();
        var tool = (dto.Tool ?? "post").Trim().ToLowerInvariant();
        if (!ValidToolPattern.Split('|').Contains(tool))
            return BadRequest(ApiResponse<SavedToolContentDto>.Error($"Tool must be one of: {ValidToolPattern}"));
        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(ApiResponse<SavedToolContentDto>.Error("Text is required"));

        var item = new SavedToolContent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Tool = tool,
            Title = dto.Title?.Trim() ?? "",
            Text = dto.Text.Trim(),
            Hashtags = dto.Hashtags?.Trim() ?? "",
            OriginalText = dto.Text.Trim(),
        };
        _db.SavedToolContent.Add(item);
        await _db.SaveChangesAsync();

        var dtoOut = Map(item);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, ApiResponse<SavedToolContentDto>.Created(dtoOut));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSavedToolContentDto dto)
    {
        var userId = GetUserId();
        var item = await _db.SavedToolContent.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (item is null) return NotFound(ApiResponse<SavedToolContentDto>.Error("Saved item not found"));

        // Adjust: the caller replaces the text with a reworked variant. OriginalText is
        // preserved forever so a bad rework can always be reverted.
        if (dto.Text is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Text))
                return BadRequest(ApiResponse<SavedToolContentDto>.Error("Text cannot be empty"));
            item.Text = dto.Text.Trim();
        }
        if (dto.Title is not null) item.Title = dto.Title.Trim();
        if (dto.Hashtags is not null) item.Hashtags = dto.Hashtags.Trim();
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<SavedToolContentDto>.Ok(Map(item)));
    }

    /// <summary>Roll an adjusted item back to its original text (undo a rework).</summary>
    [HttpPost("{id}/revert")]
    public async Task<IActionResult> Revert(Guid id)
    {
        var userId = GetUserId();
        var item = await _db.SavedToolContent.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (item is null) return NotFound(ApiResponse<SavedToolContentDto>.Error("Saved item not found"));

        item.Text = item.OriginalText;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<SavedToolContentDto>.Ok(Map(item)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var item = await _db.SavedToolContent.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (item is null) return NotFound(ApiResponse<SavedToolContentDto>.Error("Saved item not found"));
        _db.SavedToolContent.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SavedToolContentDto Map(SavedToolContent s) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        Tool = s.Tool,
        Title = s.Title,
        Text = s.Text,
        Hashtags = s.Hashtags,
        OriginalText = s.OriginalText,
        IsAdjusted = !string.Equals(s.Text, s.OriginalText, StringComparison.Ordinal),
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };
}