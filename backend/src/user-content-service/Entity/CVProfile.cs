using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace UserContentService.Entity
{
    [Table("cv_profiles")]
    public class CVProfile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty; // ex: Senior Web Developer

        [Required]
        public string Summary { get; set; } = string.Empty; // Professional Bio

        [Required]
        public Guid UserId { get; set; }


    }
}
