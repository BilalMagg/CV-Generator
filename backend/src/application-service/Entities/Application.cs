using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApplicationService.Entities;

[Table("applications")]
public class Application
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CandidateId { get; set; }

    public Guid? CvVersionId { get; set; }

    public Guid? JobOfferId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string CompanyName { get; set; }

    [Required]
    [MaxLength(150)]
    public required string PositionTitle { get; set; }

    [MaxLength(100)]
    public string? OfferSource { get; set; }

    [Required]
    [MaxLength(20)]
    public ApplicationOrigin Origin { get; set; } = ApplicationOrigin.MANUAL;

    [Required]
    public ApplicationStatus Status { get; set; } = ApplicationStatus.APPLIED;

    [Required]
    [MaxLength(280)]
    public string Fingerprint { get; set; } = string.Empty;

    public DateTime? AppliedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? Notes { get; set; }

    // Navigation
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();

    public ICollection<ApplicationAttempt> Attempts { get; set; } = new List<ApplicationAttempt>();
}
