namespace UserContentService.Events.SkillEvent
{
    public class SkillCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Level { get; set; }
        public int? YearsOfExperience { get; set; }
        public Guid? UserId { get; set; }
        public string? Category { get; set; }
    }

    public class SkillUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Level { get; set; }
        public int? YearsOfExperience { get; set; }
        public Guid? UserId { get; set; }
        public string? Category { get; set; }
    }
    public class SkillDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
    }
}
