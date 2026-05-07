using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace UserContentService.Entity;

[Table("skills")]
public class Skill
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(20)]
    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? NameEmbedding { get; set; }
}