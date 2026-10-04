namespace CV_Generator.Services;

/// <summary>
/// Thrown when a Gmail OAuth token can no longer be refreshed (Google returned
/// invalid_grant or no refresh token is stored). The connection must be re-authorized:
/// callers should surface a clear "reconnect Gmail" message instead of a generic 500.
/// </summary>
public class GmailReauthRequiredException : InvalidOperationException
{
    public GmailReauthRequiredException(string message = "Gmail authorization expired — please reconnect Gmail")
        : base(message)
    {
    }
}