namespace CV_Generator.Dto;

/// <summary>An apply pushed into the Queue instead of being sent directly.</summary>
public class CreateApplyQueueItemDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyDescription { get; set; }
    public string? Location { get; set; }
    /// <summary>Position title (falls back to the company specialty on bulk rows).</summary>
    public string? PositionTitle { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    /// <summary>Contact notes persisted on an upserted contact.</summary>
    public string? ContactNotes { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Body { get; set; }
    /// <summary>Per-item CV version; null = picked at execution time (run-level default).</summary>
    public Guid? CvVersionId { get; set; }
    /// <summary>"bulk_apply" | "quick_apply" — where the item was staged from.</summary>
    public string Source { get; set; } = "quick_apply";
}

/// <summary>One queued apply as returned to the client.</summary>
public class ApplyQueueItemDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = "quick_apply";
    public Guid ApplicationId { get; set; }
    /// <summary>Live status of the linked application (SAVED until the run sends it).</summary>
    public string? ApplicationStatus { get; set; }
    public Guid? ContactId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? PositionTitle { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Body { get; set; }
    public Guid? CvVersionId { get; set; }
    /// <summary>pending | queued | sent | failed | removed</summary>
    public string Status { get; set; } = "pending";
    public Guid? BulkRunId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}

/// <summary>Execute a selection of queue items as a bulk-apply run.</summary>
public class ExecuteApplyQueueDto
{
    public List<Guid> ItemIds { get; set; } = [];
    /// <summary>Run-level CV version for items without their own; optional.</summary>
    public Guid? CvVersionId { get; set; }
    /// <summary>Seconds between sends. Clamped to 0..120.</summary>
    public int DelaySeconds { get; set; } = 5;
}

public class ExecuteApplyQueueResult
{
    public Guid RunId { get; set; }
    public int TotalRows { get; set; }
    /// <summary>Selected items that had no recipient email (marked failed, not queued).</summary>
    public int SkippedNoEmail { get; set; }
}
