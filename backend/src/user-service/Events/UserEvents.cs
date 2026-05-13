namespace UserService.Events;

public abstract record UserEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record UserCreatedEvent : UserEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
}
