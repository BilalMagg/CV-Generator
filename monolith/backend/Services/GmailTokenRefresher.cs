using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Models;

namespace CV_Generator.Services;

public interface IGmailTokenRefresher
{
    /// <summary>
    /// Ensures the connection's access token is valid for the next few minutes:
    /// does nothing when it is still fresh, otherwise exchanges the refresh token with Google and
    /// persists the new access token + expiry. Throws <see cref="GmailReauthRequiredException"/>
    /// when the token permanently can't be refreshed (invalid_grant / missing refresh token),
    /// in which case the connection is flagged NeedsReauth.
    /// </summary>
    Task EnsureFreshAsync(GmailConnection connection, CancellationToken ct = default);
}

public class GmailTokenRefresher : IGmailTokenRefresher
{
    private const string RefreshScope = "https://www.googleapis.com/auth/gmail.send";
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IAesEncryptionService _aes;
    private readonly ILogger<GmailTokenRefresher> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public GmailTokenRefresher(
        AppDbContext db,
        IAesEncryptionService aes,
        IConfiguration config,
        ILogger<GmailTokenRefresher> logger)
    {
        _db = db;
        _aes = aes;
        _logger = logger;
        _clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID is not set");
        _clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
            ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET is not set");
    }

    public async Task EnsureFreshAsync(GmailConnection connection, CancellationToken ct = default)
    {
        if (connection.TokenExpiresAt == default ||
            DateTime.UtcNow < connection.TokenExpiresAt.Subtract(RefreshWindow))
        {
            return;
        }

        var refreshToken = _aes.Decrypt(connection.EncryptedRefreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("Gmail connection for user {UserId} has no refresh token — re-auth required", connection.UserId);
            await MarkReauthAsync(connection, "missing refresh token", ct);
            throw new GmailReauthRequiredException();
        }

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret
            },
            Scopes = new[] { RefreshScope }
        });

        try
        {
            var token = await flow.RefreshTokenAsync(refreshToken, null, ct);

            connection.EncryptedAccessToken = _aes.Encrypt(token.AccessToken);
            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
                connection.EncryptedRefreshToken = _aes.Encrypt(token.RefreshToken);
            connection.TokenExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresInSeconds ?? 3600);
            connection.NeedsReauth = false;
            connection.LastTokenErrorAt = null;
            connection.LastTokenError = null;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Refreshed Gmail token for user {UserId}", connection.UserId);
        }
        catch (TokenResponseException ex) when (IsPermanentError(ex.Error?.Error))
        {
            _logger.LogWarning(ex,
                "Gmail token refresh permanently failed ({Error}) for user {UserId} — re-auth required",
                ex.Error?.Error, connection.UserId);
            await MarkReauthAsync(connection, ex.Error?.Error ?? "invalid_grant", ct);
            throw new GmailReauthRequiredException();
        }
        catch (TokenResponseException ex)
        {
            // Any other rejection (invalid_request, invalid_token, malformed payload, ...)
            // means Google will keep refusing this token — treat it as re-auth required so
            // the send/apply paths get a handled reconnect message instead of a raw 500.
            _logger.LogWarning(ex,
                "Gmail token refresh rejected ({Error}) for user {UserId} — re-auth required",
                ex.Error?.Error, connection.UserId);
            await MarkReauthAsync(connection, ex.Error?.Error ?? "token_rejected", ct);
            throw new GmailReauthRequiredException();
        }
    }

    private static bool IsPermanentError(string? error) =>
        error is "invalid_grant" or "invalid_client" or "disabled_user" or "invalid_scope";

    private async Task MarkReauthAsync(GmailConnection connection, string error, CancellationToken ct)
    {
        connection.NeedsReauth = true;
        connection.LastTokenErrorAt = DateTime.UtcNow;
        connection.LastTokenError = error;
        await _db.SaveChangesAsync(ct);
    }
}