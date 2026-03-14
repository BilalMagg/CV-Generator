using System.ComponentModel.DataAnnotations;

namespace backend.src.features.experience.dto;

public class CreateExperienceDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = default!;

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
}

public class UpdateExperienceDto
{
    [MaxLength(150)]
    public string? Title { get; set; }

    [MaxLength(150)]
    public string? Company { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? ReferenceUrl { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }
}

public class ExperienceResponseDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = default!;

    public string? Company { get; set; }

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? ReferenceUrl { get; set; }

    public string Status { get; set; } = default!;

    public Guid UserId { get; set; }

    public string? AiSummaryJson { get; set; }
}
