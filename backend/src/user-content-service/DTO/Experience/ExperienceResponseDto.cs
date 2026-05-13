namespace UserContentService.dto.Experience
{
    public class ExperienceResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? ReferenceUrl { get; set; }
        public string Status { get; set; } = "Ongoing";
        public Guid UserId { get; set; }
    }
}
