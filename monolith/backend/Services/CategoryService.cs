using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;
using CV_Generator.Services.AgentClients;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CV_Generator.Services;

public interface ICategoryService
{
    Task<List<CategoryNodeDto>> GetTreeAsync(Guid userId, string scope);
    Task<List<CategorySearchResult>> SearchAsync(Guid userId, List<Guid> nodeIds, List<string>? sourceTypes = null);
    Task SetTagsAsync(Guid userId, string sourceType, Guid sourceId, List<Guid> nodeIds);
    Task<List<Guid>> GetTagsAsync(Guid userId, string sourceType, Guid sourceId);
    Task CategorizeEntityAsync(Guid userId, string sourceType, Guid sourceId);
}

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly ICategorizationClient _categorizationClient;

    public CategoryService(AppDbContext db, ICategorizationClient categorizationClient)
    {
        _db = db;
        _categorizationClient = categorizationClient;
    }

    public async Task<List<CategoryNodeDto>> GetTreeAsync(Guid userId, string scope)
    {
        var nodes = await _db.CategoryNodes
            .Where(n => n.Scope == scope && (n.UserId == null || n.UserId == userId))
            .ToListAsync();

        var tags = await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == scope)
            .Select(t => new { t.SourceId, t.CategoryNodeId })
            .ToListAsync();

        var byId = nodes.ToDictionary(n => n.Id);
        var counts = new Dictionary<Guid, int>();

        foreach (var tag in tags)
        {
            var current = byId.GetValueOrDefault(tag.CategoryNodeId);
            while (current != null)
            {
                counts[current.Id] = counts.GetValueOrDefault(current.Id) + 1;
                current = current.ParentId.HasValue ? byId.GetValueOrDefault(current.ParentId.Value) : null;
            }
        }

        var roots = nodes.Where(n => n.ParentId == null).OrderBy(n => n.Level).ThenBy(n => n.Name).ToList();
        return roots.Select(r => ToDto(r, byId, counts)).ToList();
    }

    private CategoryNodeDto ToDto(CategoryNode node, Dictionary<Guid, CategoryNode> byId, Dictionary<Guid, int> counts)
    {
        var children = byId.Values
            .Where(n => n.ParentId == node.Id)
            .OrderBy(n => n.Level).ThenBy(n => n.Name)
            .Select(c => ToDto(c, byId, counts))
            .ToList();

        List<string> keywords = new();
        try { keywords = JsonSerializer.Deserialize<List<string>>(node.KeywordsJson) ?? new List<string>(); }
        catch { keywords = new List<string>(); }

        return new CategoryNodeDto
        {
            Id = node.Id,
            Scope = node.Scope,
            ParentId = node.ParentId,
            Name = node.Name,
            Domain = node.Domain,
            Level = node.Level,
            Path = node.Path,
            Keywords = keywords,
            IsSystem = node.IsSystem,
            UserId = node.UserId,
            Count = counts.GetValueOrDefault(node.Id),
            Children = children,
        };
    }

    public async Task<List<CategorySearchResult>> SearchAsync(Guid userId, List<Guid> nodeIds, List<string>? sourceTypes = null)
    {
        if (nodeIds.Count == 0)
            return new List<CategorySearchResult>();

        var selected = await _db.CategoryNodes.Where(n => nodeIds.Contains(n.Id)).ToListAsync();
        var subtree = new HashSet<Guid>();
        foreach (var sel in selected)
        {
            subtree.Add(sel.Id);
            foreach (var n in await _db.CategoryNodes.Where(n => n.Path.StartsWith(sel.Path + "/")).Select(n => n.Id).ToListAsync())
                subtree.Add(n);
        }

        var query = _db.EntityCategoryTags.Where(t => t.UserId == userId && subtree.Contains(t.CategoryNodeId));
        if (sourceTypes != null && sourceTypes.Count > 0)
            query = query.Where(t => sourceTypes.Contains(t.SourceType));

        var matches = await query.Select(t => new { t.SourceId, t.SourceType, t.CategoryNodeId }).ToListAsync();

        return matches
            .GroupBy(m => new { m.SourceId, m.SourceType })
            .Select(g => new CategorySearchResult
            {
                SourceId = g.Key.SourceId,
                SourceType = g.Key.SourceType,
                Score = g.Select(x => x.CategoryNodeId).Distinct().Count(),
            })
            .OrderByDescending(r => r.Score)
            .ToList();
    }

    public async Task SetTagsAsync(Guid userId, string sourceType, Guid sourceId, List<Guid> nodeIds)
    {
        var existing = await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == sourceType && t.SourceId == sourceId)
            .ToListAsync();
        _db.EntityCategoryTags.RemoveRange(existing);

        foreach (var id in nodeIds.Distinct())
            _db.EntityCategoryTags.Add(new EntityCategoryTag
            {
                UserId = userId,
                SourceType = sourceType,
                SourceId = sourceId,
                CategoryNodeId = id,
                AssignedBy = "MANUAL",
            });

        await _db.SaveChangesAsync();
    }

    public async Task<List<Guid>> GetTagsAsync(Guid userId, string sourceType, Guid sourceId)
    {
        return await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == sourceType && t.SourceId == sourceId)
            .Select(t => t.CategoryNodeId)
            .ToListAsync();
    }

    public async Task CategorizeEntityAsync(Guid userId, string sourceType, Guid sourceId)
    {
        var text = await BuildEntityText(sourceType, sourceId);
        if (string.IsNullOrWhiteSpace(text))
            return;

        var nodes = await _db.CategoryNodes
            .Where(n => n.Scope == sourceType && (n.UserId == null || n.UserId == userId))
            .ToListAsync();

        var candidates = nodes.Select(n => new CategoryCandidateDto
        {
            Id = n.Id,
            Name = n.Name,
            Keywords = ParseKeywords(n.KeywordsJson),
        }).ToList();

        var llmIds = await _categorizationClient.CategorizeAsync(sourceType, text, candidates);
        var keywordIds = KeywordMatch(text, nodes);

        var existingManual = (await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == sourceType && t.SourceId == sourceId && t.AssignedBy == "MANUAL")
            .Select(t => t.CategoryNodeId)
            .ToListAsync()).ToHashSet();

        var toInsert = llmIds.Concat(keywordIds).Distinct().Where(id => !existingManual.Contains(id)).ToList();

        // Drop previous auto tags (keep manual), then re-insert merged auto tags.
        var autoTags = await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == sourceType && t.SourceId == sourceId && t.AssignedBy != "MANUAL")
            .ToListAsync();
        _db.EntityCategoryTags.RemoveRange(autoTags);

        var llmSet = llmIds.ToHashSet();
        foreach (var id in toInsert)
            _db.EntityCategoryTags.Add(new EntityCategoryTag
            {
                UserId = userId,
                SourceType = sourceType,
                SourceId = sourceId,
                CategoryNodeId = id,
                AssignedBy = llmSet.Contains(id) ? "LLM" : "KEYWORD",
            });

        await _db.SaveChangesAsync();
    }

    private static List<string> ParseKeywords(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    private List<Guid> KeywordMatch(string text, List<CategoryNode> nodes)
    {
        var haystack = " " + text.ToLowerInvariant() + " ";
        var result = new List<Guid>();
        foreach (var node in nodes)
        {
            var terms = new List<string> { node.Name }.Concat(ParseKeywords(node.KeywordsJson));
            foreach (var term in terms)
            {
                var t = term.Trim().ToLowerInvariant();
                if (t.Length < 3) continue;
                if (haystack.Contains(t))
                {
                    result.Add(node.Id);
                    break;
                }
            }
        }
        return result;
    }

    private async Task<string> BuildEntityText(string sourceType, Guid sourceId)
    {
        object? entity = sourceType.ToLower() switch
        {
            "projects" => await _db.Projects.FindAsync(sourceId),
            "experiences" => await _db.Experiences.FindAsync(sourceId),
            "educations" => await _db.Educations.FindAsync(sourceId),
            "certifications" => await _db.Certifications.FindAsync(sourceId),
            "skills" => await _db.Skills.FindAsync(sourceId),
            "languages" => await _db.Languages.FindAsync(sourceId),
            "hackathons" => await _db.Hackathons.FindAsync(sourceId),
            "interests" => await _db.Interests.FindAsync(sourceId),
            "academicactivities" => await _db.AcademicActivities.FindAsync(sourceId),
            _ => null,
        };
        if (entity == null) return string.Empty;

        var props = entity.GetType().GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.GetValue(entity) as string)
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(" ", props);
    }
}
