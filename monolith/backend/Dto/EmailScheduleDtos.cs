namespace CV_Generator.Dto;

public class EmailScheduleDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<Guid> RecipientIds { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateScheduleDto
{
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<Guid> RecipientIds { get; set; } = [];
}

public class UpdateScheduleDto
{
    public string? Name { get; set; }
    public string? CronExpression { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public List<Guid>? RecipientIds { get; set; }
}
