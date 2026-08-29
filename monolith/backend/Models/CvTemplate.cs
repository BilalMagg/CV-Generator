namespace CV_Generator.Models;

// CV template entity
// System templates (IsSystem=true, UserId=null) ship with the app; user templates are per-user
public class CvTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "latex"; // latex | html | pdf
    public string Content { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public Guid? UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}