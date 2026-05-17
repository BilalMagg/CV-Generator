using System.ComponentModel.DataAnnotations;

namespace UserContentService.dto.Certification
{
    public class CreateCertificationDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? IssuingOrganization { get; set; }

        public DateTime? IssueDate { get; set; }

        [MaxLength(300)]
        public string? CredentialUrl { get; set; }

    }

    public class UpdateCertificationDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? IssuingOrganization { get; set; }

        public DateTime? IssueDate { get; set; }

        [MaxLength(300)]
        public string? CredentialUrl { get; set; }
    }

    public class CertificationResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? IssuingOrganization { get; set; }
        public DateTime? IssueDate { get; set; }
        public string? CredentialUrl { get; set; }
        public Guid UserId { get; set; }
    }
}
