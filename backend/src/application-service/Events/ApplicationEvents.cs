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
    public string Status { get; init; } = "PENDING";
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