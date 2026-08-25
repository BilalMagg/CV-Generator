namespace CV_Generator.Dto;

public class ScheduleHistoryItemDto
{
    public Guid Id { get; set; }
    public string ToName { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ScheduleHistoryResponseDto
{
    public List<ScheduleHistoryItemDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

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
    public List<DateTime>? UpcomingRuns { get; set; }
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
