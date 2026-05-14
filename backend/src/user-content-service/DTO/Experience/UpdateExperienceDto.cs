
using System.ComponentModel.DataAnnotations;


namespace UserContentService.dto.Experience {

    public class UpdateExperienceDto{
        [Required]
        [MaxLength(150)]
        public required string Title { get; set; }

        [MaxLength(150)]
        public string? Company { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [MaxLength(300)]
        public string? ReferenceUrl { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Ongoing";
    }
     

}