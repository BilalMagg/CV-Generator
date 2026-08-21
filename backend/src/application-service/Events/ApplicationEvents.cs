namespace ApplicationService.Events;

public abstract record ApplicationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record ApplicationCreatedEvent : ApplicationEvent
{
    public Guid ApplicationId { get; init; }
    public Guid CandidateId { get; init; }
    public string CompanyName { get; init; } = "";
    public string PositionTitle { get; init; } = "";
    public string Status { get; init; } = "APPLIED";
}

public record ApplicationStatusUpdatedEvent : ApplicationEvent
{
    public Guid ApplicationId { get; init; }
    public string OldStatus { get; init; } = "";
    public string NewStatus { get; init; } = "";
    public string? ChangedBy { get; init; }
    public string? Comment { get; init; }
}

public record ApplicationDeletedEvent : ApplicationEvent
{
    public Guid ApplicationId { get; init; }
    public Guid CandidateId { get; init; }
}

public record ApplicationUpdatedEvent : ApplicationEvent
{
    public Guid ApplicationId { get; init; }
    public Guid CandidateId { get; init; }
    public string CompanyName { get; init; } = "";
    public string PositionTitle { get; init; } = "";
    public string? UpdatedBy { get; init; }
}

public record ApplicationAttemptCreatedEvent : ApplicationEvent
{
    public Guid AttemptId { get; init; }
    public Guid ApplicationId { get; init; }
    public int AttemptNumber { get; init; }
    public string Channel { get; init; } = "";
    public string InitiatedBy { get; init; } = "USER";
    public string Status { get; init; } = "DRAFT";
}

public record ApplicationAttemptUpdatedEvent : ApplicationEvent
{
    public Guid AttemptId { get; init; }
    public Guid ApplicationId { get; init; }
    public int AttemptNumber { get; init; }
    public string OldStatus { get; init; } = "";
    public string NewStatus { get; init; } = "";
}
