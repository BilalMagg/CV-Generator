using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

[Table("bime_conversations")]
public class BimeConversation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid UserId { get; set; }
    [MaxLength(200)]
    public string Title { get; set; } = "New conversation";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
