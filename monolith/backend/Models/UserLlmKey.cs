using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CV_Generator.Models;

/// <summary>A per-user LLM provider API key stored in-app (encrypted at rest),
/// pushed to the sidecar which consults it before falling back to env keys.
/// These deployments are single-tenant (one instance owner), so key resolution
/// in the engine is global rather than threaded per request.</summary>
[Table("user_llm_keys")]
public class UserLlmKey
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = "";

    /// <summary>AES-encrypted plaintext API key.</summary>
    [Required]
    [MaxLength(2000)]
    public string KeyCipher { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
