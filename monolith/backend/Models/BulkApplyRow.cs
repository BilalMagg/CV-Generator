using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

/// <summary>One company/recipient line inside a <see cref="BulkApplyRun"/>.</summary>
[Table("bulk_apply_rows")]
public class BulkApplyRow
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid RunId { get; set; }

    public BulkApplyRun? Run { get; set; }

    public int SortOrder { get; set; }

    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? Specialty { get; set; }

    [MaxLength(320)]
    public string? RecipientEmail { get; set; }

    [MaxLength(200)]
    public string? RecipientName { get; set; }

    [MaxLength(500)]
    public string? Subject { get; set; }

    public string? Body { get; set; }

    /// <summary>pending | sending | sent | failed | skipped</summary>
    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "pending";

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public Guid? ApplicationId { get; set; }

    public Guid? ContactId { get; set; }

    public Guid? CompanyId { get; set; }

    public DateTime? SentAt { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
