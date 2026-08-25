using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

[Table("companies")]
public class Company
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? WebsiteUrl { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? LocationUrl { get; set; }

    public string? Note { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
