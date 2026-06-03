using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkflowService.Entity;

[Table("cv_generation_runs")]
public class CvGenerationRun
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string JobDescription { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CandidateName { get; set; }

    [MaxLength(300)]
    public string? RecipientEmail { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    public int CurrentStep { get; set; }

    public string? StepStatuses { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ExtractionResult { get; set; }
    public string? SearchResult { get; set; }
    public string? OptimizationResult { get; set; }
    public string? RenderResult { get; set; }
    public string? DeliveryResult { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
