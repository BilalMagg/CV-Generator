using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.src.features.user.entity;
using Pgvector;

namespace backend.src.features.project.entity
{
    [Table("projects")]
    public class Project
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid(); // Unique project ID

        [Required]
        [MaxLength(150)]
        public required string Title { get; set; } // Project name / title

        [MaxLength(1000)]
        public string? Description { get; set; } // Short description

        [MaxLength(500)]
        public string? Role { get; set; } // Role in the project (e.g., frontend dev, AI dev)

        [MaxLength(1000)]
        public string? Achievements { get; set; } // Optional key achievements

        [Required]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; } // Nullable, in case project is ongoing

        // Optional link to external resources
        [MaxLength(300)]
        public string? RepositoryUrl { get; set; }

        [MaxLength(300)]
        public string? DemoUrl { get; set; }

        // Project status (ongoing / completed / paused)
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Ongoing";

        // Foreign Key: user who owns/created this project
        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        // Optional: project tags or skills (JSON for scalability)
        public string? SkillsJson { get; set; }

        // Optional: AI-generated summary or notes
        public string? AiSummaryJson { get; set; }

        // Vector embedding for RAG search (pgvector)
        public Vector? DescriptionEmbedding { get; set; }
    }
}