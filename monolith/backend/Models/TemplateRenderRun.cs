using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

// A single "render CV from template" job: takes a persisted job-extraction
// output, matches the user's profile, renders against a template (content +
// placeholder conditions) and optionally compiles a PDF + saves to Documents.
// Drives the dedicated Template Agent page (SSE progress).
[Table("template_render_runs")]
public class TemplateRenderRun
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid ExtractionId { get; set; }

    // Snapshot of the extraction output (denormalized so result/SSE assembly
    // never needs to re-read the job_extractions row).
    public string? ExtractionJson { get; set; }

    [MaxLength(50)]
    public string? TemplateId { get; set; }

    [MaxLength(10)]
    public string? Language { get; set; }

    [MaxLength(30)]
    public string? Tone { get; set; }

    public bool SaveToDocuments { get; set; } = true;

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    public int CurrentStep { get; set; }

    public string? StepStatuses { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SearchResult { get; set; }

    public string? RenderResult { get; set; }

    public string? PdfUrl { get; set; }

    public Guid? CvId { get; set; }

    public Guid? CvVersionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}