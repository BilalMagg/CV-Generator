using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UserService.Entity;

[Table("users")]
[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string KeycloakId { get; set; } = default!;

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

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public DateTime? BirthDate { get; set; }

    [Required]
    [MaxLength(20)]
    public Role Role { get; set; } = Role.USER;

    [MaxLength(255)]
    public string? AvatarUrl { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLogin { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    public string? AiProfileDataJson { get; set; }
    public string? PreferencesJson { get; set; }
}

public enum Role
{
    USER,
    ADMIN,
}