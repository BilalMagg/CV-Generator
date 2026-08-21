namespace CV_Generator.Models;

public class GmailConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string GmailAddress { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
}
