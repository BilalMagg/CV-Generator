
namespace JobOfferService.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("job_skills")]
public class JobSkill
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid JobOfferId { get; set; }

    [Required]
    [MaxLength(150)]
    public required string Name { get; set; }

    [Required]
    public SkillType Type { get; set; }

    [Required]
    public bool IsMandatory { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(JobOfferId))]
    public JobOffer? JobOffer { get; set; }
}