using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Models;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkillsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SkillsController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public SkillsController(AppDbContext db, ILogger<SkillsController> logger, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        userId ??= CurrentUserId;

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
            UserId = RequiredUserId,
            Category = dto.Category,
            Subcategory = dto.Subcategory,
            LastUsedYear = dto.LastUsedYear,
            IsCore = dto.IsCore,
            SortOrder = dto.SortOrder
        };

        var dupCheck = await IsDuplicateAsync(skill.UserId, skill.Name);
        if (dupCheck ?? true) return Conflict(ApiResponse<Skill>.Error($"A skill named \"{skill.Name}\" already exists"));

        _db.Skills.Add(skill);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Skills_UserId_NameNormalized", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Conflict(ApiResponse<Skill>.Error($"A skill named \"{skill.Name}\" already exists"));
        }
        SearchSyncHelper.TriggerSync(_scopeFactory, skill.UserId, _logger, "Skill.Create", skill.Id);

        _logger.LogInformation("Created skill {Id}", skill.Id);
        return Created($"/api/skills/{skill.Id}", ApiResponse<Skill>.Created(skill));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSkillDto dto)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<Skill>.Error("Skill not found"));

        var dupCheck = await IsDuplicateAsync(skill.UserId, dto.Name, id);
        if (dupCheck ?? true) return Conflict(ApiResponse<Skill>.Error($"A skill named \"{dto.Name}\" already exists"));

        skill.Name = dto.Name;
        skill.Level = dto.Level;
        skill.YearsOfExperience = dto.YearsOfExperience;
        skill.Category = dto.Category;
        skill.Subcategory = dto.Subcategory;
        skill.LastUsedYear = dto.LastUsedYear;
        skill.IsCore = dto.IsCore;
        skill.SortOrder = dto.SortOrder;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Skills_UserId_NameNormalized", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Conflict(ApiResponse<Skill>.Error($"A skill named \"{dto.Name}\" already exists"));
        }
        SearchSyncHelper.TriggerSync(_scopeFactory, skill.UserId, _logger, "Skill.Update", skill.Id);
        return Ok(ApiResponse<Skill>.Ok(skill));
    }

    /// <summary>True/False when a same-named skill exists for the user; null when the name is blank.</summary>
    private async Task<bool?> IsDuplicateAsync(Guid userId, string name, Guid? selfId = null)
    {
        var key = Normalize(name);
        if (key.Length == 0) return null;
        return await _db.Skills.AnyAsync(s => s.UserId == userId && s.Id != selfId && Normalize(s.Name) == key);
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkSkillsRequest req)
    {
        if (req?.Ids is null || req.Ids.Count == 0)
            return BadRequest(ApiResponse<object>.Error("No skills selected"));

        var userId = RequiredUserId;
        var ids = req.Ids.Distinct().ToList();
        var skills = await _db.Skills.Where(s => s.UserId == userId && ids.Contains(s.Id)).ToListAsync();
        if (skills.Count == 0) return NotFound(ApiResponse<object>.Error("No matching skills"));

        var skillIds = skills.Select(s => s.Id).ToList();
        var tags = await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == "skills" && skillIds.Contains(t.SourceId))
            .ToListAsync();
        _db.EntityCategoryTags.RemoveRange(tags);
        _db.Skills.RemoveRange(skills);
        await _db.SaveChangesAsync();

        foreach (var s in skills) SearchSyncHelper.TriggerSync(_scopeFactory, userId, _logger, "Skill.Delete", s.Id);
        return Ok(ApiResponse<object>.Ok(new { deleted = skills.Count }, $"{skills.Count} skills deleted"));
    }

    /// <summary>
    /// Swaps the category for many skills at once (keeps every other field untouched).
    /// Category set to an existing root → re-tagged to it; a new name → the container is
    /// created (same behaviour as import); null/empty → skills become uncategorized.
    /// </summary>
    [HttpPost("category")]
    public async Task<IActionResult> MoveCategory([FromBody] SkillCategoryMoveRequest req)
    {
        if (req?.Ids is null || req.Ids.Count == 0)
            return BadRequest(ApiResponse<object>.Error("No skills selected"));

        var userId = RequiredUserId;
        var ids = req.Ids.Distinct().ToList();
        var skills = await _db.Skills.Where(s => s.UserId == userId && ids.Contains(s.Id)).ToListAsync();
        if (skills.Count == 0) return NotFound(ApiResponse<object>.Error("No matching skills"));

        var target = req.Category?.Trim();
        Guid? nodeId = null;
        if (!string.IsNullOrEmpty(target))
        {
            var node = await _db.CategoryNodes.FirstOrDefaultAsync(n =>
                n.Scope == "skills" && n.ParentId == null && (n.UserId == null || n.UserId == userId) &&
                n.Name != null && Normalize(n.Name) == Normalize(target));
            if (node == null)
            {
                node = new CategoryNode
                {
                    Id = Guid.NewGuid(),
                    Scope = "skills",
                    ParentId = null,
                    Name = target,
                    Domain = target,
                    Level = 0,
                    Path = "/" + Slug(target),
                    KeywordsJson = "[]",
                    IsSystem = false,
                    UserId = userId,
                };
                _db.CategoryNodes.Add(node);
            }
            nodeId = node.Id;
        }

        var skillIds = skills.Select(s => s.Id).ToList();
        var existingTags = await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == "skills" && skillIds.Contains(t.SourceId))
            .ToListAsync();
        _db.EntityCategoryTags.RemoveRange(existingTags);

        foreach (var s in skills)
        {
            s.Category = string.IsNullOrEmpty(target) ? null : target;
            if (nodeId.HasValue)
                _db.EntityCategoryTags.Add(new EntityCategoryTag
                {
                    UserId = userId,
                    SourceType = "skills",
                    SourceId = s.Id,
                    CategoryNodeId = nodeId.Value,
                    AssignedBy = "MANUAL",
                });
        }
        await _db.SaveChangesAsync();

        foreach (var s in skills) SearchSyncHelper.TriggerSync(_scopeFactory, userId, _logger, "Skill.Update", s.Id);
        return Ok(ApiResponse<object>.Ok(new { updated = skills.Count, category = string.IsNullOrEmpty(target) ? null : target }));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<object>.Error("Skill not found"));

        _db.Skills.Remove(skill);
        await _db.SaveChangesAsync();
        SearchSyncHelper.TriggerSync(_scopeFactory, skill.UserId, _logger, "Skill.Delete", skill.Id);
        return NoContent();
    }

    private static string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();

    private static string Slug(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in s.Trim().ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
    }

    public record CreateSkillDto(string Name, string? Level, int? YearsOfExperience, Guid UserId, string? Category, string? Subcategory = null, int? LastUsedYear = null, bool IsCore = false, int SortOrder = 0);
    public record UpdateSkillDto(string Name, string? Level, int? YearsOfExperience, string? Category, string? Subcategory = null, int? LastUsedYear = null, bool IsCore = false, int SortOrder = 0);
    public record BulkSkillsRequest(List<Guid> Ids);
    public record SkillCategoryMoveRequest(List<Guid> Ids, string? Category);
}
