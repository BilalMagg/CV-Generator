namespace CvService.Entities;

// CV section entity
// Stores individual sections of a CV version
public class CvSection
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public string SectionType { get; set; } = string.Empty; // personal_info, experience, education, skills, projects, etc.
    public int DisplayOrder { get; set; }
    public string ContentJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public CvVersion Version { get; set; } = null!;
}
