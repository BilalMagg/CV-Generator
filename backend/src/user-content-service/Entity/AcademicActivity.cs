using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserContentService.Entity
{
    [Table("academic_activities")]
    public class AcademicActivity
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty; // e.g. "Club President", "Volunteer"
        
        public string? Organization { get; set; } // Club or school name
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
    }
}
