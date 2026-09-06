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
    Task<List<CategorySearchResult>> SearchByTextAsync(Guid userId, List<string> scopes, string text);
    Task SetTagsAsync(Guid userId, string sourceType, Guid sourceId, List<Guid> nodeIds);
    Task<List<Guid>> GetTagsAsync(Guid userId, string sourceType, Guid sourceId);
    Task<List<ScopeTagsDto>> GetTagsForScopeAsync(Guid userId, string sourceType);
    Task CategorizeEntityAsync(Guid userId, string sourceType, Guid sourceId);
    Task<List<Guid>> SuggestEntityAsync(Guid userId, string sourceType, Guid sourceId);
    Task<int> CategorizeScopeAsync(Guid userId, string scope);
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

    /// <summary>
    /// Deterministic taxonomy search driven by free text (no LLM): keyword-matches the text
    /// against the user's category nodes (name + keywords, per scope) for the given scopes,
    /// then returns the tagged entities ranked by distinct matched-node count.
    /// Used to surface the most job-relevant skills/experiences/projects without a model call.
    /// </summary>
    public async Task<List<CategorySearchResult>> SearchByTextAsync(Guid userId, List<string> scopes, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || scopes == null || scopes.Count == 0)
            return new List<CategorySearchResult>();

        var matched = new HashSet<Guid>();
        foreach (var scope in scopes)
        {
            var nodes = await _db.CategoryNodes
                .Where(n => n.Scope == scope && (n.UserId == null || n.UserId == userId))
                .ToListAsync();
            foreach (var id in KeywordMatch(text, nodes))
                matched.Add(id);
        }

        if (matched.Count == 0)
            return new List<CategorySearchResult>();

        return await SearchAsync(userId, matched.ToList(), scopes);
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

    public async Task<List<ScopeTagsDto>> GetTagsForScopeAsync(Guid userId, string sourceType)
    {
        var rows = await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == sourceType)
            .Select(t => new { t.SourceId, t.CategoryNodeId })
            .ToListAsync();

        return rows
            .GroupBy(r => r.SourceId)
            .Select(g => new ScopeTagsDto
            {
                SourceId = g.Key,
                NodeIds = g.Select(x => x.CategoryNodeId).ToList(),
            })
            .ToList();
    }

    public async Task CategorizeEntityAsync(Guid userId, string sourceType, Guid sourceId)
    {
        var (toInsert, llmSet) = await ComputeSuggestionsAsync(userId, sourceType, sourceId);
        if (toInsert.Count == 0)
            return;

        // Drop previous auto tags (keep manual), then re-insert merged auto tags.
        var autoTags = await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == sourceType && t.SourceId == sourceId && t.AssignedBy != "MANUAL")
            .ToListAsync();
        _db.EntityCategoryTags.RemoveRange(autoTags);

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

    public async Task<List<Guid>> SuggestEntityAsync(Guid userId, string sourceType, Guid sourceId)
    {
        var (toInsert, _) = await ComputeSuggestionsAsync(userId, sourceType, sourceId);
        return toInsert;
    }

    /// <summary>
    /// Compute the suggested category node ids for an entity (LLM + keyword, excluding existing
    /// MANUAL tags). Does NOT persist anything. Returns the merged ids plus the subset that came
    /// from the LLM (used by CategorizeEntityAsync to tag the source). Returns empty when the
    /// entity has no textual content to categorize.
    /// </summary>
    private async Task<(List<Guid> toInsert, HashSet<Guid> llmSet)> ComputeSuggestionsAsync(Guid userId, string sourceType, Guid sourceId)
    {
        var text = await BuildEntityText(sourceType, sourceId);
        if (string.IsNullOrWhiteSpace(text))
            return (new List<Guid>(), new HashSet<Guid>());

        var nodes = await _db.CategoryNodes
            .Where(n => n.Scope == sourceType && (n.UserId == null || n.UserId == userId))
            .ToListAsync();

        var candidates = nodes.Select(n => new CategoryCandidateDto
        {
            Id = n.Id,
            Name = n.Name,
            Keywords = ParseKeywords(n.KeywordsJson),
            Path = n.Path,
        }).ToList();

        var llmIds = await _categorizationClient.CategorizeAsync(sourceType, text, candidates);
        var keywordIds = KeywordMatch(text, nodes);

        var existingManual = (await _db.EntityCategoryTags
            .Where(t => t.UserId == userId && t.SourceType == sourceType && t.SourceId == sourceId && t.AssignedBy == "MANUAL")
            .Select(t => t.CategoryNodeId)
            .ToListAsync()).ToHashSet();

        var merged = llmIds.Concat(keywordIds).Distinct().Where(id => !existingManual.Contains(id)).ToList();
        // A category node may be a parent; for a single entity we always store its leaf
        // descendants so the tag is specific and shows correctly in the tree.
        var toInsert = ExpandToLeaves(merged, nodes);
        var llmSet = ComputeLlmSourcedSet(toInsert, llmIds, nodes);
        return (toInsert, llmSet);
    }

    /// <summary>Replace every selected node id with its leaf descendants (a leaf returns itself).</summary>
    private List<Guid> ExpandToLeaves(List<Guid> ids, List<CategoryNode> nodes)
    {
        var childrenByParent = nodes
            .Where(n => n.ParentId.HasValue)
            .GroupBy(n => n.ParentId.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        var leafCache = new Dictionary<Guid, List<Guid>>();

        List<Guid> GetLeaves(Guid id)
        {
            if (leafCache.TryGetValue(id, out var cached)) return cached;
            if (!childrenByParent.TryGetValue(id, out var kids))
            {
                var single = new List<Guid> { id };
                leafCache[id] = single;
                return single;
            }
            var leaves = new List<Guid>();
            foreach (var k in kids)
                leaves.AddRange(GetLeaves(k.Id));
            leafCache[id] = leaves;
            return leaves;
        }

        var result = new HashSet<Guid>();
        foreach (var id in ids)
            foreach (var leaf in GetLeaves(id))
                result.Add(leaf);
        return result.ToList();
    }

    /// <summary>A leaf is LLM-sourced if it (or any of its ancestors) was chosen by the LLM.</summary>
    private HashSet<Guid> ComputeLlmSourcedSet(List<Guid> expanded, List<Guid> originalLlm, List<CategoryNode> nodes)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var llmSet = new HashSet<Guid>();
        foreach (var leaf in expanded)
        {
            var cur = byId.GetValueOrDefault(leaf);
            while (cur != null)
            {
                if (originalLlm.Contains(cur.Id)) { llmSet.Add(leaf); break; }
                cur = cur.ParentId.HasValue ? byId.GetValueOrDefault(cur.ParentId.Value) : null;
            }
        }
        return llmSet;
    }

    private static List<string> ParseKeywords(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    private List<Guid> KeywordMatch(string text, List<CategoryNode> nodes)
    {
        var haystack = " " + text.ToLowerInvariant() + " ";
        var entityTokens = text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', ',', ';', '.', '(', ')', '-', '/', '\\', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToHashSet();
        var result = new List<Guid>();
        foreach (var node in nodes)
        {
            var terms = new List<string> { node.Name }.Concat(ParseKeywords(node.KeywordsJson));
            foreach (var term in terms)
            {
                var t = term.Trim().ToLowerInvariant();
                if (t.Length < 2) continue;
                // whole-term presence in the entity text
                if (haystack.Contains(" " + t + " ") || haystack.Contains(t))
                {
                    result.Add(node.Id);
                    break;
                }
                // token-level overlap (e.g. entity "aws" vs node keyword "aws", or
                // entity "learning" vs node "machine learning")
                if (entityTokens.Any(et => t.Contains(et) || et.Contains(t)))
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

    public async Task<int> CategorizeScopeAsync(Guid userId, string scope)
    {
        var ids = await GetEntityIdsForScope(userId, scope);
        foreach (var id in ids)
            await CategorizeEntityAsync(userId, scope, id);
        return ids.Count;
    }

    private async Task<List<Guid>> GetEntityIdsForScope(Guid userId, string scope)
    {
        return scope.ToLower() switch
        {
            "projects" => await _db.Projects.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(),
            "experiences" => await _db.Experiences.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(),
            "educations" => await _db.Educations.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(),
            "certifications" => await _db.Certifications.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(),
            "skills" => await _db.Skills.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(),
            "languages" => await _db.Languages.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(),
            "hackathons" => await _db.Hackathons.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(),
            "interests" => await _db.Interests.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(),
            "academicactivities" => await _db.AcademicActivities.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(),
            _ => new List<Guid>(),
        };
    }
}
