namespace NotificationService.Application.DTOs;

public class ReminderResultDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime EventDate { get; set; }
    public string ReminderOffset { get; set; } = string.Empty;
    public DateTime ReminderAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
