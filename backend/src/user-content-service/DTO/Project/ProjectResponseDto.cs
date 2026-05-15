namespace UserContentService.dto.Project
{
    public class ProjectResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Role { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? RepositoryUrl { get; set; }
        public string? DemoUrl { get; set; }
        public string Status { get; set; } = "Completed";
        public Guid UserId { get; set; }
        public string? SkillsJson { get; set; }
    }
}
