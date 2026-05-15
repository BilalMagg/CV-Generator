using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.Experience {

    public class CreateExperienceDto{
     
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

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Ongoing";

    [Required]
    public Guid UserId { get; set; }

    }
}