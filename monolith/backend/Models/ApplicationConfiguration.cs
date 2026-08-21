using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

[Table("application_configurations")]
public class ApplicationConfiguration
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    public bool ShowReminders { get; set; } = true;

    [MaxLength(500)]
    public string? SelectedStatuses { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
