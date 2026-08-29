namespace CV_Generator.Dto;

/// <summary>Internal + wire request for generating answers to an external application-form's fields.</summary>
public class ApplyPrepFormRequest
{
    public string UserId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string? JobRole { get; set; }
    public string? CompanyDescription { get; set; }
    public string? JobDescription { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> Responsibilities { get; set; } = new();
    public string Language { get; set; } = "English";
    public List<string> Fields { get; set; } = new();
    public bool SaveTracked { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? ContactNotes { get; set; }
    public Guid? CvVersionId { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
}

/// <summary>Internal + wire request for generating a direct outreach message (LinkedIn/WhatsApp/...).</summary>
public class ApplyPrepMessageRequest
{
    public string UserId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string? JobRole { get; set; }
    public string? CompanyDescription { get; set; }
    public string? JobDescription { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> Responsibilities { get; set; } = new();
    public string Language { get; set; } = "English";
    public string Channel { get; set; } = "LinkedIn";
    public string? Considerations { get; set; }
    public bool SaveTracked { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? ContactNotes { get; set; }
    public Guid? CvVersionId { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
}

/// <summary>Minimal payload to create a tracked SAVED application (no send) for prep flows.</summary>
public class ApplyPrepTrackedRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string? CompanyDescription { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? ContactNotes { get; set; }
    public Guid? CvVersionId { get; set; }
}

public class FormResponseItemDto
{
    public string Field { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class ApplyPrepFormResult
{
    public Guid? ApplicationId { get; set; }
    public List<FormResponseItemDto> Responses { get; set; } = new();
}

public class ApplyPrepMessageResult
{
    public Guid? ApplicationId { get; set; }
    public string Message { get; set; } = string.Empty;
}
