using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

[Table("bime_messages")]
public class BimeMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid ConversationId { get; set; }
    [MaxLength(20)]
    public required string Role { get; set; } // "user" | "assistant"
    public required string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ConversationId))]
    public BimeConversation? Conversation { get; set; }
}
