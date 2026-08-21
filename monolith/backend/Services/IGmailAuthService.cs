namespace CV_Generator.Services;

public interface IGmailAuthService
{
    string GetOAuthUrl(Guid userId);
    Task HandleCallbackAsync(string code, string state);
    Task<GmailConnectionStatus?> GetStatusAsync(Guid userId);
    Task DisconnectAsync(Guid userId);
}

public class GmailConnectionStatus
{
    public string Email { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
}
