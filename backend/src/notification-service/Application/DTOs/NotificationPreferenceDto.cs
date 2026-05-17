namespace NotificationService.Application.DTOs;

public class NotificationPreferenceDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool EnableEmail { get; set; }
    public bool EnableInApp { get; set; }
    public bool Reminders { get; set; }
    public bool ApplicationUpdates { get; set; }
    public bool CvUpdates { get; set; }
    public bool WeeklyDigest { get; set; }
    public int DefaultReminderDaysBefore { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateNotificationPreferenceDto
{
    public bool? EnableEmail { get; set; }
    public bool? EnableInApp { get; set; }
    public bool? Reminders { get; set; }
    public bool? ApplicationUpdates { get; set; }
    public bool? CvUpdates { get; set; }
    public bool? WeeklyDigest { get; set; }
    public int? DefaultReminderDaysBefore { get; set; }
}
