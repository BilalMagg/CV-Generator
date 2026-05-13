namespace UserContentService.Events.CVProfileEvent
{
    public class CVProfileCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public Guid UserId { get; set; }
    }

    public class CVProfileUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public Guid UserId { get; set; }
    }
    public class CVProfileDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
