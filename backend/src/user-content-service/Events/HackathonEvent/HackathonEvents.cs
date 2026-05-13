namespace UserContentService.Events.HackathonEvent
{
    public class HackathonCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Organization { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public string? Result { get; set; }
        public Guid UserId { get; set; }
    }

    public class HackathonUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Organization { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public string? Result { get; set; }
        public Guid UserId { get; set; }
    }
    public class HackathonDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
