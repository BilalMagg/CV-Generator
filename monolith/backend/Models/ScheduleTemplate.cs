using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

[Table("schedule_templates")]
public class ScheduleTemplate
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    [Required]
    [MaxLength(500)]
    public required string SubjectTemplate { get; set; }

    [Required]
    public required string BodyTemplate { get; set; }

    public string? VariableDefaultsJson { get; set; }

    public Guid? CvVersionId { get; set; }

    public string? AttachmentRefsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}