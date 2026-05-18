using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.Education
{
    public class CreateEducationDto
    {
        [Required]
        [MaxLength(150)]
        public string InstitutionName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string DegreeType { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FieldOfStudy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Specialization { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Ongoing";

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(300)]
        public string? DiplomaFileUrl { get; set; }

    }
}
