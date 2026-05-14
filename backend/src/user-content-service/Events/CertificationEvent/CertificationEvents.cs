namespace UserContentService.Events.CertificationEvent
{
    public class CertificationCreatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? IssuingOrganization { get; set; }
        public DateTime? IssueDate { get; set; }
        public string? CredentialUrl { get; set; }
        public Guid UserId { get; set; }
    }

    public class CertificationUpdatedEvent
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? IssuingOrganization { get; set; }
        public DateTime? IssueDate { get; set; }
        public string? CredentialUrl { get; set; }
        public Guid UserId { get; set; }
    }
    public class CertificationDeletedEvent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
    }
}
