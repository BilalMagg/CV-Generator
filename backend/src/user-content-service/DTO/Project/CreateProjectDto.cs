using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.Project{

public class CreateProjectDto{
 [Required]
    [MaxLength(150)]
    public required string Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Role { get; set; }

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


    public string? SkillsJson { get; set; }
}
}