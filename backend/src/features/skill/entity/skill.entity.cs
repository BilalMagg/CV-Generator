using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.src.features.user.entity;

namespace backend.src.features.skill.entity
{
    [Table("skills")]
    public class Skill
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public required string Name { get; set; } // e.g., "C#", "Angular", "Communication"

        [MaxLength(50)]
        public string? Level { get; set; } // Beginner / Intermediate / Advanced / Expert

        // Optional years of experience
        public int? YearsOfExperience { get; set; }

        // Optional link to user who owns this skill
        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Optional: category (technical, soft, language...)
        [MaxLength(50)]
        public string? Category { get; set; }
    }
}