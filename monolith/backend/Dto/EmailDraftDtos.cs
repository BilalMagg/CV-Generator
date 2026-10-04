namespace CV_Generator.Dto;

/// <summary>Summary row for the draft picker list (no body/description payload).</summary>
public class EmailDraftListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? PositionTitle { get; set; }
    public string? RecipientEmail { get; set; }
    public string? Subject { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Full draft payload (includes body + attachments) for the detail view.</summary>
public class EmailDraftDto : EmailDraftListItemDto
{
    public string? CompanyDescription { get; set; }
    public string? RecipientName { get; set; }
    public string? ContactNotes { get; set; }
    public string? Body { get; set; }
    public Guid? CvVersionId { get; set; }
    public string? CvTitle { get; set; }
    public List<ScheduleAttachmentRef> Attachments { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public bool HasCv { get; set; }
    public bool HasAttachments { get; set; }
}

public class CreateEmailDraftDto
{
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? PositionTitle { get; set; }
    public string? CompanyDescription { get; set; }
    public string? RecipientEmail { get; set; }
    public string? RecipientName { get; set; }
    public string? ContactNotes { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public Guid? CvVersionId { get; set; }
    public List<EmailAttachmentDto>? Attachments { get; set; }
}

public class UpdateEmailDraftDto
{
    public string? Name { get; set; }
    public string? CompanyName { get; set; }
    public string? PositionTitle { get; set; }
    public string? CompanyDescription { get; set; }
    public string? RecipientEmail { get; set; }
    public string? RecipientName { get; set; }
    public string? ContactNotes { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }

    /// <summary>Guid.Empty clears the CV; absent keeps the current one.</summary>
    public Guid? CvVersionId { get; set; }

    /// <summary>null keeps existing attachments; a (possibly empty) list replaces them.</summary>
    public List<EmailAttachmentDto>? Attachments { get; set; }
}