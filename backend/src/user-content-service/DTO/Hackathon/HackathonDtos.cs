using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.Hackathon
{
    public class CreateHackathonDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public string? Organization { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public string? Result { get; set; }
    }

    public class UpdateHackathonDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public string? Organization { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public string? Result { get; set; }
    }

    public class HackathonResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Organization { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public string? Result { get; set; }
        public Guid UserId { get; set; }
    }
}
