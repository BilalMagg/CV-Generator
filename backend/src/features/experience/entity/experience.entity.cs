using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.src.features.user.entity;

namespace backend.src.features.experience.entity
{
    [Table("experiences")]
    public class Experience
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(150)]
        public required string Title { get; set; } // Job title or role

        [MaxLength(150)]
        public string? Company { get; set; } // Company / Organization name

        [MaxLength(500)]
        public string? Description { get; set; } // Short description of tasks

        [Required]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; } // Nullable if ongoing

        // Optional link to reference or portfolio
        [MaxLength(300)]
        public string? ReferenceUrl { get; set; }

        // Status: ongoing / completed
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Ongoing";

        // Foreign key to the user
        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Optional: AI-generated summary or notes
        public string? AiSummaryJson { get; set; }
    }
}