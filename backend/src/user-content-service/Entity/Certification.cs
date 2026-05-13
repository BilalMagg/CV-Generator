using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace UserContentService.Entity
{
    [Table("certifications")]
    public class Certification
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? IssuingOrganization { get; set; }

        public DateTime? IssueDate { get; set; }

        [MaxLength(300)]
        public string? CredentialUrl { get; set; }

        [Required]
        public Guid UserId { get; set; }


    }
}
