using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserContentService.Entity
{
    [Table("languages")]
    public class Language
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Level { get; set; } = string.Empty; // ex: Native, B2, C1

        [Required]
        public Guid UserId { get; set; }
    }
}
