using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.CVProfile
{
    public class CreateCVProfileDto
    {
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Summary { get; set; } = string.Empty;

    }

    public class UpdateCVProfileDto
    {
        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Summary { get; set; } = string.Empty;
    }

    public class CVProfileResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public Guid UserId { get; set; }
    }
}
