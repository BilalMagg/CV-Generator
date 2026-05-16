using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.Interest
{
    public class CreateInterestDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

    }

    public class UpdateInterestDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class InterestResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid UserId { get; set; }
    }
}
