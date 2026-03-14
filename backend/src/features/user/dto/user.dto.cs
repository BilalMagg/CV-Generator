using System.ComponentModel.DataAnnotations;

namespace backend.src.features.user.dto;

public class CreateUserDto
{
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = default!;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;

    public string? PhoneNumber { get; set; }

    public DateTime? BirthDate { get; set; }
}

public class UpdateUserDto
{
    [MaxLength(50)]
    public string? FirstName { get; set; }

    [MaxLength(50)]
    public string? LastName { get; set; }

    public string? PhoneNumber { get; set; }

    public DateTime? BirthDate { get; set; }

    public string? AvatarUrl { get; set; }
}

public class UserResponseDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = default!;

    public string LastName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string Role { get; set; } = default!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
