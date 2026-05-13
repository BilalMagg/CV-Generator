namespace JobOfferService.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("job_benefits")]
public class JobBenefit
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid JobOfferId { get; set; }

    [Required]
    [MaxLength(300)]
    public required string Description { get; set; }

    // Navigation
    [ForeignKey(nameof(JobOfferId))]
    public JobOffer? JobOffer { get; set; }
}