using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApplicationService.Entities;

[Table("application_status_history")]
public class ApplicationStatusHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ApplicationId { get; set; }

    [ForeignKey(nameof(ApplicationId))]
    public Application? Application { get; set; }

    public ApplicationStatus? OldStatus { get; set; }

    [Required]
    public ApplicationStatus NewStatus { get; set; }

    [Required]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ChangedBy { get; set; }

    public string? Comment { get; set; }
}