using System.Text.Json;

namespace CV_Generator.Models;

public class EmailSchedule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public List<Guid> RecipientIds { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Guid? ApplicationId { get; set; }
    public Guid? CvVersionId { get; set; }
    public Guid? TemplateSourceId { get; set; }
    public string? AttachmentRefsJson { get; set; }
}
