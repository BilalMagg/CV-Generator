using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.Language
{
    public class CreateLanguageDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Level { get; set; } = string.Empty;

    }

    public class UpdateLanguageDto
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Level { get; set; } = string.Empty;
    }

    public class LanguageResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public Guid UserId { get; set; }
    }
}
