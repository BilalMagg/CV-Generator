using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector; // Required for pgvector support


namespace JobOfferService.Entities;

[Table("job_offers")]
public class JobOffer
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string EnterpriseName { get; set; }

    public string? EnterpriseDescription { get; set; }

    [Required]
    [MaxLength(150)]
    public required string JobRole { get; set; }

    [Required]
    public required string RawDescription { get; set; }

    // Maps to pgvector extension in PostgreSQL
    [Column(TypeName = "vector(1536)")] 
    public Vector? DescriptionVector { get; set; }

    public int? RequiredExperienceYears { get; set; }

    [MaxLength(100)]
    public string? SeniorityLevel { get; set; }

    [MaxLength(100)]
    public string? EmploymentType { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(100)]
    public string? LocationType { get; set; }

    public string? EducationRequirements { get; set; }

    [MaxLength(500)]
    public string? SourceUrl { get; set; }
    
    [Required]
    public JobOfferStatus Status { get; set; } = JobOfferStatus.OPEN;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<JobSkill> Skills { get; set; } = new List<JobSkill>();
    public ICollection<JobResponsibility> Responsibilities { get; set; } = new List<JobResponsibility>();
    public ICollection<JobBenefit> Benefits { get; set; } = new List<JobBenefit>();
}

