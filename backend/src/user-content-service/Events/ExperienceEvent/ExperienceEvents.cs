namespace UserContentService.Events.ExperienceEvent
{
    public class ExperienceCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Company { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? ReferenceUrl { get; set; }
        public string? Status { get; set; }
        public Guid UserId { get; set; }
    }

    public class ExperienceUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Company { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? ReferenceUrl { get; set; }
        public string? Status { get; set; }
        public Guid UserId { get; set; }
    }
    public class ExperienceDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
