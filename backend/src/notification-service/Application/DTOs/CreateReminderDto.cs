namespace NotificationService.Application.DTOs;

public class CreateReminderDto
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFirstName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }

    /// <summary>The actual event date (e.g. interview, deadline)</summary>
    public DateTime EventDate { get; set; }

    /// <summary>How many days before the event to send the reminder.
    /// Values: "None", "OneDay", "TwoDays", "ThreeDays", "OneWeek"</summary>
    public string ReminderOffset { get; set; } = "OneDay";
}
