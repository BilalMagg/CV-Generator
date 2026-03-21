using System.ComponentModel.DataAnnotations;
using Pgvector;

namespace backend.src.features.skill.dto;

public class CreateSkillDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;

    [MaxLength(50)]
    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }
}

public class UpdateSkillDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(50)]
    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }
}

public class SkillResponseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }

    public Guid UserId { get; set; }

    public string? Category { get; set; }

    /// <summary>384-dim pgvector embedding of the skill name. Used by the Python RAG agent.</summary>
    public Vector? NameEmbedding { get; set; }
}
