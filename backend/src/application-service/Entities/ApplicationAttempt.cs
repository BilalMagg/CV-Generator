using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApplicationService.Entities;

[Table("application_attempts")]
public class ApplicationAttempt
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ApplicationId { get; set; }

    [ForeignKey(nameof(ApplicationId))]
    public Application? Application { get; set; }

    public int AttemptNumber { get; set; } = 1;

    [Required]
    [MaxLength(30)]
    public AttemptChannel Channel { get; set; } = AttemptChannel.EMAIL_GMAIL;

    [Required]
    [MaxLength(20)]
    public AttemptInitiatedBy InitiatedBy { get; set; } = AttemptInitiatedBy.USER;

    [Required]
    [MaxLength(20)]
    public AttemptStatus Status { get; set; } = AttemptStatus.DRAFT;

    [MaxLength(300)]
    public string? Subject { get; set; }

    public string? Body { get; set; }

    [MaxLength(200)]
    public string? RecipientName { get; set; }

    [MaxLength(300)]
    public string? RecipientContact { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ChannelMetadataJson { get; set; }

    public Guid? CvVersionId { get; set; }

    public DateTime? SentAt { get; set; }

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum AttemptChannel
{
    EMAIL_GMAIL,
    EMAIL_SMTP,
    WHATSAPP,
    LINKEDIN_MESSAGE,
    LINKEDIN_CONNECTION,
    WEB_FORM,
    IN_PERSON,
    OTHER
}

public enum AttemptInitiatedBy
{
    USER,
    AI_AGENT,
    SCHEDULE
}

public enum AttemptStatus
{
    DRAFT,
    SCHEDULED,
    SENT,
    FAILED
}
