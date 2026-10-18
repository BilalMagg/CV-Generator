using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

/// <summary>
/// One bulk-apply campaign: a batch of companies/emails that is sent sequentially by a
/// background worker with a configurable delay between rows. Progress is polled by the
/// client so a closed tab never loses the run.
/// </summary>
[Table("bulk_apply_runs")]
public class BulkApplyRun
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    /// <summary>pending | running | completed | failed | cancelled</summary>
    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "pending";

    public int TotalRows { get; set; }

    public int ProcessedRows { get; set; }

    public int SentCount { get; set; }

    public int FailedCount { get; set; }

    public int SkippedCount { get; set; }

    /// <summary>Seconds to wait between each successful send (pacing).</summary>
    public int DelaySeconds { get; set; } = 5;

    /// <summary>CV version attached to every email in the run (optional).</summary>
    public Guid? CvVersionId { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public List<BulkApplyRow> Rows { get; set; } = [];
}
