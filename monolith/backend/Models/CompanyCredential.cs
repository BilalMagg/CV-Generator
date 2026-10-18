using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

/// <summary>
/// A portal/account login kept for a company (some applications require creating
/// an account on the employer's site before you can apply).
/// </summary>
[Table("company_credentials")]
public class CompanyCredential
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    [Required]
    public Guid UserId { get; set; }

    /// <summary>What this account is for, e.g. "Taleo", "Workday", "Greenhouse".</summary>
    [MaxLength(200)]
    public string? Label { get; set; }

    /// <summary>Login page URL.</summary>
    [MaxLength(500)]
    public string? Url { get; set; }

    [MaxLength(300)]
    public string? Username { get; set; }

    [MaxLength(500)]
    public string? Password { get; set; }

    /// <summary>2FA recovery codes, how the account was created, expected response…</summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
