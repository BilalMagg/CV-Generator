namespace CV_Generator.Services;

public static class SearchSyncHelper
{
    private static readonly Dictionary<string, string> ScopeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Project"] = "projects",
        ["Experience"] = "experiences",
        ["Education"] = "educations",
        ["Certification"] = "certifications",
        ["Skill"] = "skills",
        ["Language"] = "languages",
        ["Hackathon"] = "hackathons",
        ["Interest"] = "interests",
        ["AcademicActivity"] = "academicactivities",
    };

    /// <summary>
    /// Auto-categorize an entity after it changes (hybrid LLM + keyword tagging,
    /// preserving manual tags). Embedding/vector sync was removed 2026-10-04 —
    /// discovery is category-tree taxonomy based, so this now only maintains
    /// category tags.
    /// </summary>
    public static void TriggerSync(IServiceScopeFactory scopeFactory, Guid userId, ILogger logger, string source, Guid entityId = default)
    {
        if (entityId == default ||
            source.EndsWith(".Create", StringComparison.Ordinal) ||
            !ScopeMap.TryGetValue(source.Split('.')[0], out var scopeName))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var categoryService = scope.ServiceProvider.GetRequiredService<ICategoryService>();
                await categoryService.CategorizeEntityAsync(userId, scopeName, entityId);
                logger.LogDebug("Category tags updated for user {UserId} entity {Scope}/{EntityId} after {Source}", userId, scopeName, entityId, source);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Category tagging skipped for user {UserId} entity {Scope}/{EntityId}", userId, scopeName, entityId);
            }
        });
    }
}