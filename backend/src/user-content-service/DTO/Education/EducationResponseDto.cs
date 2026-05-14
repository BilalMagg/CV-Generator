namespace UserContentService.dto.Education
{
    public class EducationResponseDto
    {
        public Guid Id { get; set; }
        public string InstitutionName { get; set; } = string.Empty;
        public string DegreeType { get; set; } = string.Empty;
        public string FieldOfStudy { get; set; } = string.Empty;
        public string? Specialization { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "Ongoing";
        public string? City { get; set; }
        public string? DiplomaFileUrl { get; set; }
        public Guid UserId { get; set; }
    }
}
