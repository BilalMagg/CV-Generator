namespace CV_Generator.Models;

/// <summary>
/// A per-user assignment of an entity to a category node. Replaces embeddings as the primary search signal.
/// </summary>
public class EntityCategoryTag
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Entity-type scope, matches CategoryNode.Scope.</summary>
    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }
    public Guid CategoryNodeId { get; set; }

    /// <summary>LLM | KEYWORD | MANUAL — provenance; MANUAL tags are preserved across auto re-syncs.</summary>
    public string AssignedBy { get; set; } = "LLM";

    public CategoryNode? CategoryNode { get; set; }
}
