using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace UserContentService.Entity;

[Table("experiences")]
public class Experience
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public required string Title { get; set; }

    [MaxLength(150)]
    public string? Company { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? ReferenceUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Ongoing";

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public string? AiSummaryJson { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? DescriptionEmbedding { get; set; }
}