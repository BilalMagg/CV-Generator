namespace UserContentService.Events.SocialLinkEvent
{
    public class SocialLinkCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Platform { get; set; }
        public string? Url { get; set; }
        public Guid UserId { get; set; }
    }

    public class SocialLinkUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Platform { get; set; }
        public string? Url { get; set; }
        public Guid UserId { get; set; }
    }
    public class SocialLinkDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
