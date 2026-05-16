namespace NotificationService.Domain.Entities;

public class NotificationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public bool EnableEmail { get; set; } = true;
    public bool EnableInApp { get; set; } = true;
    public bool Reminders { get; set; } = true;
    public bool ApplicationUpdates { get; set; } = true;
    public bool CvUpdates { get; set; } = true;
    public bool WeeklyDigest { get; set; } = true;
    public int DefaultReminderDaysBefore { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
