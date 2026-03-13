using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.src.features.auth.entity;
using Microsoft.EntityFrameworkCore;

namespace backend.src.features.user.entity
{
    [Table("users")]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid(); // Id unique global

        [Required]
        [MaxLength(50)]
        public required string FirstName { get; set; }

        [Required]
        [MaxLength(50)]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public required string Email { get; set; }

        [Required]
        public required string PasswordHash { get; set; } // Toujours stocker le hash

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public DateTime? BirthDate { get; set; }

        // Rôle pour gérer permissions (Admin / User / Recruiter ...)
        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "User";

        // Avatar / profile picture
        [MaxLength(255)]
        public string? AvatarUrl { get; set; }

        // Date d'inscription / création du compte
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLogin { get; set; }

        // Statut actif ou désactivé
        [Required]
        public bool IsActive { get; set; } = true;

        // Informations pour RAG / AI (optionnel, JSON ou table séparée)
        public string? AiProfileDataJson { get; set; }

        // Preferences de l'utilisateur (JSON pour scalabilité)
        public string? PreferencesJson { get; set; }

        // Relation avec CVs (1 user peut avoir plusieurs CV)
        // public virtual ICollection<CV> CVs { get; set; }

        // Relation avec Applications (1 user peut avoir plusieurs candidatures)
        // public virtual ICollection<Application> Applications { get; set; }

        // Tokens de sécurité (optionnel)
        public virtual ICollection<UserToken>? Tokens { get; set; }
        public virtual ICollection<PasswordResetToken>? PasswordResetTokens { get; set; }
        public virtual ICollection<LoginAttempt>? LoginAttempts { get; set; }
    }
}