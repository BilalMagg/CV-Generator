namespace UserContentService.Events.InterestEvent
{
    public class InterestCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public Guid UserId { get; set; }
    }

    public class InterestUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public Guid UserId { get; set; }
    }
    public class InterestDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
