namespace CV_Generator.Dto;

/// <summary>Paste blob to extract bulk-apply rows from (deterministic parse first, LLM fallback).</summary>
public class BulkExtractRequestDto
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public string? Provider { get; set; }
    public string? Model { get; set; }
}

/// <summary>One company/recipient extracted from the pasted text.</summary>
public class BulkExtractRowDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Email { get; set; }
    public string? ContactName { get; set; }
    public List<string> Specialties { get; set; } = [];
}

public class BulkExtractResultDto
{
    /// <summary>"deterministic" | "llm"</summary>
    public string Method { get; set; } = "deterministic";
    public List<BulkExtractRowDto> Rows { get; set; } = [];
}

/// <summary>One resolved line to send (subject/body already have tokens resolved client-side).</summary>
public class BulkApplyRowInputDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Specialty { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class CreateBulkApplyRunDto
{
    public Guid? CvVersionId { get; set; }
    /// <summary>Seconds between sends. Clamped to 0..120.</summary>
    public int DelaySeconds { get; set; } = 5;
    public List<BulkApplyRowInputDto> Rows { get; set; } = [];
}

public class BulkApplySubmitResponse
{
    public Guid RunId { get; set; }
    public int TotalRows { get; set; }
}

public class BulkApplyRowDto
{
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Specialty { get; set; }
    public string? RecipientEmail { get; set; }
    public string? RecipientName { get; set; }
    public string? Subject { get; set; }
    public string Status { get; set; } = "pending";
    public string? ErrorMessage { get; set; }
    public Guid? ApplicationId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? CompanyId { get; set; }
    public DateTime? SentAt { get; set; }
}

public class BulkApplyRunDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "pending";
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public int DelaySeconds { get; set; }
    public Guid? CvVersionId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<BulkApplyRowDto> Rows { get; set; } = [];
}
