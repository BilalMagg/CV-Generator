using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.SocialLink
{
    public class CreateSocialLinkDto
    {
        [Required]
        [MaxLength(50)]
        public string Platform { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Url { get; set; } = string.Empty;

        [Required]
        public Guid UserId { get; set; }
    }

    public class UpdateSocialLinkDto
    {
        [Required]
        [MaxLength(50)]
        public string Platform { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Url { get; set; } = string.Empty;
    }

    public class SocialLinkResponseDto
    {
        public Guid Id { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public Guid UserId { get; set; }
    }
}
