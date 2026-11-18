namespace CV_Generator.Services;

public interface IGmailAuthService
{
    string GetOAuthUrl(Guid userId, string? returnUrl = null);
    string GenerateSignedState(Guid userId, string? returnUrl = null);
    bool TryValidateState(string state, out Guid userId, out string returnUrl);
    Task HandleCallbackAsync(string code, string state);
    Task<GmailConnectionStatus?> GetStatusAsync(Guid userId);
    Task DisconnectAsync(Guid userId);
}

public class GmailConnectionStatus
{
    public string Email { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public bool NeedsReauth { get; set; }
}
