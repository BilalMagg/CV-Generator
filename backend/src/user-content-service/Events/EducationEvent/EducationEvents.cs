namespace UserContentService.Events.EducationEvent
{
    public class EducationCreatedEvent
    {
        public Guid Id { get; set; }
        public string? InstitutionName { get; set; }
        public string? DegreeType { get; set; }
        public string? FieldOfStudy { get; set; }
        public string? Specialization { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Status { get; set; }
        public string? City { get; set; }
        public string? DiplomaFileUrl { get; set; }
        public Guid UserId { get; set; }
    }

    public class EducationUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? InstitutionName { get; set; }
        public string? DegreeType { get; set; }
        public string? FieldOfStudy { get; set; }
        public string? Specialization { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Status { get; set; }
        public string? City { get; set; }
        public string? DiplomaFileUrl { get; set; }
        public Guid UserId { get; set; }
    }
    public class EducationDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
