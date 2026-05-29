using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkflowService.Entity;

[Table("job_extractions")]
public class JobExtractionEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }

    [MaxLength(20)]
    public string InputType { get; set; } = string.Empty;

    [MaxLength(300)]
    public string InputSummary { get; set; } = string.Empty;

    public string OutputJson { get; set; } = string.Empty;

    [MaxLength(200)]
    public string JobRole { get; set; } = string.Empty;

    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
