namespace NotificationService.Domain.Enums;

public enum ReminderOffset
{
    None,       // Remind on the event date itself
    OneDay,     // 1 day before
    TwoDays,    // 2 days before
    ThreeDays,  // 3 days before
    OneWeek     // 7 days before
}
