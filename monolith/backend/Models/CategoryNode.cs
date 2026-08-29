namespace CV_Generator.Models;

/// <summary>
/// A node in a per-entity-type category taxonomy (e.g. Projects / Experiences / Certifications).
/// The tree is shared (system-seeded) and users may add their own nodes (UserId set).
/// </summary>
public class CategoryNode
{
    public Guid Id { get; set; }

    /// <summary>Entity-type scope, matches the user-content route key (projects, experiences, certifications, ...).</summary>
    public string Scope { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Top-level domain (Technical / Architectural / DevOps / Security / ...), denormalized for fast filtering.</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Depth from the root (root = 0).</summary>
    public int Level { get; set; }

    /// <summary>Materialized path of ancestor names, e.g. "/devops/cloud/aws" — enables O(1) subtree queries.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>JSON array of synonyms/aliases used for deterministic query resolution and tagging, e.g. ["AWS","EC2","S3"].</summary>
    public string KeywordsJson { get; set; } = "[]";

    public bool IsSystem { get; set; } = true;

    /// <summary>Null for the shared system seed; set for a user-added node.</summary>
    public Guid? UserId { get; set; }

    public CategoryNode? Parent { get; set; }
    public ICollection<CategoryNode> Children { get; set; } = new List<CategoryNode>();
}
