using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using CV_Generator.Data;
using CV_Generator.Models;
using CV_Generator.Services;

namespace CV_Generator.Services;

public class GmailSendService : IGmailSendService
{
    private readonly AppDbContext _db;
    private readonly IAesEncryptionService _aes;
    private readonly IGmailTokenRefresher _tokenRefresher;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GmailSendService> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public GmailSendService(
        AppDbContext db,
        IAesEncryptionService aes,
        IGmailTokenRefresher tokenRefresher,
        IHttpClientFactory httpClientFactory,
        ILogger<GmailSendService> logger)
    {
        _db = db;
        _aes = aes;
        _tokenRefresher = tokenRefresher;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID is not set");
        _clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
            ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET is not set");
    }

    public async Task<(string? MessageId, string? ThreadId)> SendWithAttachmentAsync(
        Guid userId, string to, string subject, string body,
        string? cvPdfUrl = null, string? cvTitle = null, IReadOnlyList<CV_Generator.Dto.EmailAttachmentDto>? attachments = null)
    {
        var connection = await _db.Set<GmailConnection>()
            .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsRevoked);

        if (connection is null)
        {
            throw new InvalidOperationException("Gmail not connected for user");
        }

        await _tokenRefresher.EnsureFreshAsync(connection);

        var credential = BuildCredential(connection);

        var gmailService = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "CV_Generator"
        });

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("", connection.GmailAddress));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { TextBody = body };

        if (!string.IsNullOrWhiteSpace(cvPdfUrl))
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                var pdfBytes = await httpClient.GetByteArrayAsync(cvPdfUrl);
                var fileName = SanitizeFileName(cvTitle, fallbackUrl: cvPdfUrl, fallbackName: "cv.pdf");

                bodyBuilder.Attachments.Add(fileName, pdfBytes, ContentType.Parse("application/pdf"));
                _logger.LogInformation("Attached PDF from {Url} ({Size} bytes)", cvPdfUrl, pdfBytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download PDF from {Url}, sending without attachment", cvPdfUrl);
            }
        }

        if (attachments is not null)
        {
            foreach (var att in attachments)
            {
                if (string.IsNullOrWhiteSpace(att.FileName) || string.IsNullOrWhiteSpace(att.ContentBase64))
                    continue;
                try
                {
                    var bytes = Convert.FromBase64String(att.ContentBase64);
                    var contentType = ContentType.Parse(
                        string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType);
                    bodyBuilder.Attachments.Add(att.FileName, bytes, contentType);
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning(ex, "Skipping attachment {FileName}: invalid base64", att.FileName);
                }
            }
        }

        message.Body = bodyBuilder.ToMessageBody();

        using var rawStream = new MemoryStream();
        message.WriteTo(rawStream);
        var raw = Convert.ToBase64String(rawStream.ToArray());

        var gmailMessage = new Message { Raw = raw };

        Message sent;
        try
        {
            sent = await gmailService.Users.Messages.Send(gmailMessage, "me").ExecuteAsync();
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // The access token died between refresh and send (expired/revoked mid-compose).
            _logger.LogWarning(ex, "Gmail send rejected with 401 for user {UserId} — re-auth required", userId);
            throw new GmailReauthRequiredException();
        }
        catch (Google.GoogleApiException ex)
        {
            throw new InvalidOperationException($"Gmail send failed: {ex.Message}", ex);
        }

        _logger.LogInformation(
            "Gmail sent via {From} to {To} | Subject: {Subject} | Attached: {HasPdf} | MessageId: {MessageId}",
            connection.GmailAddress, to, subject, !string.IsNullOrWhiteSpace(cvPdfUrl), sent.Id);

        return (sent.Id, sent.ThreadId);
    }

    private UserCredential BuildCredential(GmailConnection connection)
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _clientId,
                ClientSecret = _clientSecret
            },
            Scopes = new[] { "https://www.googleapis.com/auth/gmail.send" }
        });

        var tokenResponse = new TokenResponse
        {
            AccessToken = _aes.Decrypt(connection.EncryptedAccessToken),
            RefreshToken = _aes.Decrypt(connection.EncryptedRefreshToken),
            ExpiresInSeconds = (long)(connection.TokenExpiresAt - DateTime.UtcNow).TotalSeconds,
            IssuedUtc = DateTime.UtcNow
        };

        return new UserCredential(flow, connection.UserId.ToString(), tokenResponse);
    }

    /// <summary>
    /// Produces a human-readable PDF filename from the CV title.
    /// Falls back to extracting the name from the MinIO URL, then to a default.
    /// </summary>
    private static string SanitizeFileName(string? cvTitle, string? fallbackUrl, string fallbackName = "CV.pdf")
    {
        var name = string.IsNullOrWhiteSpace(cvTitle) ? null : cvTitle.Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            var invalid = Path.GetInvalidFileNameChars();
            name = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        }
        if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(fallbackUrl))
        {
            try { name = Path.GetFileName(new Uri(fallbackUrl).AbsolutePath); } catch { /* ignore bad URL */ }
        }
        if (string.IsNullOrWhiteSpace(name)) name = fallbackName;
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name += ".pdf";
        return name;
    }
}
