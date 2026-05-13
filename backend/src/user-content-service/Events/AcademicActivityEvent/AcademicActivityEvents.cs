namespace UserContentService.Events.AcademicActivityEvent
{
    public class AcademicActivityCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Organization { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Guid UserId { get; set; }
    }

    public class AcademicActivityUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Organization { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Guid UserId { get; set; }
    }
    public class AcademicActivityDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
