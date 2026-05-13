namespace UserContentService.Events.LanguageEvent
{
    public class LanguageCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Level { get; set; }
        public Guid UserId { get; set; }
    }

    public class LanguageUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Level { get; set; }
        public Guid UserId { get; set; }
    }
    public class LanguageDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
