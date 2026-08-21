namespace CV_Generator.Models;

public class EmailMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ContactId { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "sent";
    public string? Error { get; set; }
    public string Provider { get; set; } = "gmail";
    public DateTime? SentAt { get; set; }
    public Guid? ScheduleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contact? Contact { get; set; }
}
