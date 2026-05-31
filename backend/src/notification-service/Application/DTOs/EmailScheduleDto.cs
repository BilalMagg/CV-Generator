namespace NotificationService.Application.DTOs;

public class EmailScheduleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Cron { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string RecipientValue { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateScheduleDto
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Cron { get; set; } = string.Empty;
    public string RecipientType { get; set; } = "contacts";
    public string RecipientValue { get; set; } = string.Empty;
}

public class UpdateScheduleDto
{
    public string? Name { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? Cron { get; set; }
    public string? RecipientType { get; set; }
    public string? RecipientValue { get; set; }
    public bool? IsActive { get; set; }
}
