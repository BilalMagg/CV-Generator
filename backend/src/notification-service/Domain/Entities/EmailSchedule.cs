namespace NotificationService.Domain.Entities;

public class EmailSchedule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Cron { get; set; } = string.Empty;
    public string RecipientType { get; set; } = "contacts";
    public string RecipientValue { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
