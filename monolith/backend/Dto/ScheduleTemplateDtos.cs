using System.Text.Json.Serialization;

namespace CV_Generator.Dto;

/// <summary>Default values used to fill {{tokens}} when a template is applied to a company
/// that lacks the corresponding data.</summary>
public class ScheduleVariableDefaults
{
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("company_description")]
    public string? CompanyDescription { get; set; }

    [JsonPropertyName("my_name")]
    public string? MyName { get; set; }

    [JsonPropertyName("my_phone")]
    public string? MyPhone { get; set; }

    [JsonPropertyName("my_email")]
    public string? MyEmail { get; set; }
}

/// <summary>A file stored in MinIO and attached to a template/schedule at fire time.</summary>
public class ScheduleAttachmentRef
{
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class ScheduleTemplateDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public ScheduleVariableDefaults? VariableDefaults { get; set; }
    public Guid? CvVersionId { get; set; }
    public List<ScheduleAttachmentRef> Attachments { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateScheduleTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public ScheduleVariableDefaults? VariableDefaults { get; set; }
    public Guid? CvVersionId { get; set; }
    public List<ScheduleAttachmentRef>? Attachments { get; set; }
}

public class UpdateScheduleTemplateDto
{
    public string? Name { get; set; }
    public string? SubjectTemplate { get; set; }
    public string? BodyTemplate { get; set; }
    public ScheduleVariableDefaults? VariableDefaults { get; set; }
    public Guid? CvVersionId { get; set; }
    public List<ScheduleAttachmentRef>? Attachments { get; set; }
}

/// <summary>Applies a reusable ScheduleTemplate to one company, producing a concrete
/// per-company EmailSchedule with its own period/send time.</summary>
public class ApplyTemplateDto
{
    public Guid TemplateId { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyDescription { get; set; }
    public Guid? RecipientContactId { get; set; }
    public string? RecipientEmail { get; set; }
    public string? RecipientName { get; set; }
    public string? ContactNotes { get; set; }
    public string? CronExpression { get; set; }
    public string? ScheduleName { get; set; }
    public bool CreateCompanyIfMissing { get; set; } = true;
}

public class ApplyTemplateResultDto
{
    public Guid ScheduleId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ContactId { get; set; }
}

/// <summary>One-stop wizard payload: paste-and-apply an email to a job post.</summary>
public class ApplyEmailRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string? CompanyDescription { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string? ContactNotes { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? CvVersionId { get; set; }
    public List<EmailAttachmentDto>? Attachments { get; set; }

    /// <summary>When set, the email is scheduled (cron) instead of sent immediately.</summary>
    public string? ScheduleCron { get; set; }
    public string? ScheduleName { get; set; }

    public bool AllowDuplicate { get; set; }
}

public class ApplyEmailResult
{
    public Guid ApplicationId { get; set; }
    public Guid? ScheduleId { get; set; }
    public Guid? AttemptId { get; set; }
    public Guid ContactId { get; set; }
    public Guid CompanyId { get; set; }
    public bool SentNow { get; set; }
}