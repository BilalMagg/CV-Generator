namespace UserContentService.Events.ProjectEvent
{
    public class ProjectCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public string? Achievements { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? RepositoryUrl { get; set; }
        public string? DemoUrl { get; set; }
        public string? Status { get; set; }
        public string? SkillsJson { get; set; }
        public Guid UserId { get; set; }
    }

    public class ProjectUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public string? Achievements { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? RepositoryUrl { get; set; }
        public string? DemoUrl { get; set; }
        public string? Status { get; set; }
        public string? SkillsJson { get; set; }
        public Guid UserId { get; set; }
    }
    public class ProjectDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
