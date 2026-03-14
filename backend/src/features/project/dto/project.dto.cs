using System.ComponentModel.DataAnnotations;

namespace backend.src.features.project.dto;

public class CreateProjectDto
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = default!;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
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
}

public class UpdateProjectDto
{
    [MaxLength(150)]
    public string? Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Role { get; set; }

    [MaxLength(1000)]
    public string? Achievements { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? RepositoryUrl { get; set; }

    [MaxLength(300)]
    public string? DemoUrl { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }

    public string? SkillsJson { get; set; }
}

public class ProjectResponseDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = default!;

    public string? Description { get; set; }

    public string? Role { get; set; }

    public string? Achievements { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? RepositoryUrl { get; set; }

    public string? DemoUrl { get; set; }

    public string Status { get; set; } = default!;

    public Guid UserId { get; set; }

    public string? SkillsJson { get; set; }

    public string? AiSummaryJson { get; set; }
}
