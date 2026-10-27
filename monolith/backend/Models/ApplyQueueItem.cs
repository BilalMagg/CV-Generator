using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

/// <summary>
/// One staged apply inside the Queue: a composed email (company/recipient/subject/body/CV)
/// waiting to be executed as a bulk-apply run. Created <b>before</b> anything is sent — the
/// tracking shell (Company + optional Contact + SAVED Application) already exists and is
/// referenced by <see cref="ApplicationId"/>, so the queued apply is visible in the kanban
/// as a SAVED card. Executing pushes the item into a <see cref="BulkApplyRun"/> row, and the
/// send attaches its attempt to that already-existing application.
/// </summary>
[Table("apply_queue_items")]
public class ApplyQueueItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    /// <summary>How the item got in: "bulk_apply" | "quick_apply".</summary>
    [Required]
    [MaxLength(30)]
    public string Source { get; set; } = "quick_apply";

    /// <summary>Saved application created at queue time (status SAVED until the send flips it).</summary>
    [Required]
    public Guid ApplicationId { get; set; }

    public Application? Application { get; set; }

    /// <summary>Contact created/augmented at queue time; optional when only an email is known.</summary>
    public Guid? ContactId { get; set; }

    /// <summary>Company row created/upserted at queue time.</summary>
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Location { get; set; }

    /// <summary>Position title (maps to the bulk row's Specialty).</summary>
    [MaxLength(500)]
    public string? PositionTitle { get; set; }

    [Required]
    [MaxLength(320)]
    public string RecipientEmail { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? RecipientName { get; set; }

    /// <summary>Subject with tokens already resolved client-side (same contract as bulk rows).</summary>
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    public string? Body { get; set; }

    /// <summary>Per-item CV version; null falls back to the run-level CV chosen at execution.</summary>
    public Guid? CvVersionId { get; set; }

    /// <summary>pending | queued | sent | failed | removed</summary>
    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "pending";

    /// <summary>Bulk-apply run that consumed this item (set when queued).</summary>
    public Guid? BulkRunId { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
