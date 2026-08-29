using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

[Table("user_llm_settings")]
public class UserLlmSettings
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    [MaxLength(50)]
    public string Provider { get; set; } = "";

    [MaxLength(200)]
    public string Model { get; set; } = "";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
