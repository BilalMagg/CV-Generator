namespace CvService.Entities;

// CV entity
// Represents a user's CV document
public class Cv
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }

    public List<CvVersion> Versions { get; set; } = new();
}
