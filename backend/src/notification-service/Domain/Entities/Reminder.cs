using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFirstName { get; set; } = string.Empty;

    /// <summary>What the reminder is about, e.g. "Follow up with Google"</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional custom note the user wants included in the email</summary>
    public string? Message { get; set; }

    /// <summary>The actual event date (interview, deadline, etc.)</summary>
    public DateTime EventDate { get; set; }

    /// <summary>How many days before EventDate the user wants the reminder</summary>
    public ReminderOffset ReminderOffset { get; set; } = ReminderOffset.OneDay;

    /// <summary>Computed: EventDate minus the offset. This is when the email fires.</summary>
    public DateTime ReminderAt { get; set; }

    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
