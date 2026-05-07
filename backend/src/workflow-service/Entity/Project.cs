using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace WorkflowService.Entity;

[Table("projects")]
public class Project
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public required string Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Role { get; set; }

    [MaxLength(1000)]
    public string? Achievements { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? RepositoryUrl { get; set; }

    [MaxLength(300)]
    public string? DemoUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Ongoing";

    [Required]
    public Guid UserId { get; set; }

    public string? SkillsJson { get; set; }
    public string? AiSummaryJson { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? DescriptionEmbedding { get; set; }
}
