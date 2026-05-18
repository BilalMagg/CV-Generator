namespace CvService.Events;
public abstract record CVEvent
{
  public Guid EventId {get; init;} = Guid.NewGuid();
  public DateTime OccurredAt {get; init;} = DateTime.UtcNow;
}

public record CVCreatedEvent :CVEvent
{
  public Guid CVId {get; init;}
  public Guid UserId {get; init;}
  public string Title {get; init;} = "";
  public string TemplateId {get; init;} ="";
}

public record CVUpdateEvent : CVEvent
{
  public Guid CVId {get; init;}
  public Guid UserId {get; init;}
}
public record CVDeletedEvent: CVEvent
{
  public Guid CVId {get; init;}
  public Guid UserId {get; init;}
}