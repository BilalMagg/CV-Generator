namespace JobOfferService.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("job_responsibilities")]
public class JobResponsibility
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid JobOfferId { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Description { get; set; }

    // Navigation
    [ForeignKey(nameof(JobOfferId))]
    public JobOffer? JobOffer { get; set; }
}