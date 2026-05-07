using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;
using Pgvector;

namespace WorkflowService.Entity;

public class AgentDocumentChunk
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Column(TypeName = "vector(768)")]
    public Vector? Embedding { get; set; }

    public NpgsqlTsVector? SearchVector { get; set; }
}
