using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.src.features.user.entity;

namespace backend.src.features.auth.entity
{
    // -----------------------------
    // Token de session / JWT
    // -----------------------------
    public class UserToken
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(500)]
        public required string Token { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        public bool IsRevoked { get; set; } = false;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // -----------------------------
    // Token pour reset mot de passe
    // -----------------------------
    public class PasswordResetToken
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(500)]
        public required string Token { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        public bool Used { get; set; } = false;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // -----------------------------
    // Tentatives de connexion (optionnel)
    // -----------------------------
    public class LoginAttempt
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public bool Success { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(200)]
        public string? UserAgent { get; set; }
    }
}