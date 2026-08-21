namespace CV_Generator;

public record UserCreatedEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName
);

public record UserRegisteredEvent(
    Guid EventId,
    DateTime OccurredAt,
    string? InternalUserId,
    string Email,
    string FirstName,
    string LastName
);
