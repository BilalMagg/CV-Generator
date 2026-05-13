using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserContentService.Entity
{
    [Table("hackathons")]
    public class Hackathon
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        public string? Organization { get; set; }
        public DateTime? Date { get; set; }
        public string? Description { get; set; }
        public string? Role { get; set; }
        public string? Result { get; set; } // e.g. "Winner", "Finalist"
        
        [Required]
        public Guid UserId { get; set; }
    }
}
