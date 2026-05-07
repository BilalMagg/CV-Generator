namespace CvService.Entities;

// CV version entity
// Each CV can have multiple versions (drafts, revisions)
public class CvVersion
{
    public Guid Id { get; set; }
    public Guid CvId { get; set; }
    public int VersionNumber { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? PdfUrl { get; set; }
    public string ContentJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Cv Cv { get; set; } = null!;
}
