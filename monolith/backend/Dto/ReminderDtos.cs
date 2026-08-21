namespace CV_Generator.Dto;

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

public class CreateReminderDto
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFirstName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime EventDate { get; set; }
    public string ReminderOffset { get; set; } = "OneDay";
}
