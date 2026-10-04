using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

/// <summary>
/// A reusable application email composition saved from the Apply wizard ("Save as draft").
/// Standalone — it is NOT linked to an application; the wizard loads it and fills the compose
/// fields so the user can send/schedule through the normal flow.
/// </summary>
[Table("email_drafts")]
public class EmailDraft
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(120)]
    public required string Name { get; set; }

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(150)]
    public string? PositionTitle { get; set; }

    public string? CompanyDescription { get; set; }

    [MaxLength(200)]
    public string? RecipientEmail { get; set; }

    [MaxLength(150)]
    public string? RecipientName { get; set; }

    public string? ContactNotes { get; set; }

    [MaxLength(500)]
    public string? Subject { get; set; }

    public string? Body { get; set; }

    public Guid? CvVersionId { get; set; }

    /// <summary>Denormalized CV title for display (kept in sync on create/update).</summary>
    [MaxLength(200)]
    public string? CvTitle { get; set; }

    /// <summary>jsonb — serialized <see cref="ScheduleAttachmentRef"/> list (MinIO refs).</summary>
    public string? AttachmentsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}