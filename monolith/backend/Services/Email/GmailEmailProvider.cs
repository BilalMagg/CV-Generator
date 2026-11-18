namespace CV_Generator.Services.Email;

public class GmailEmailProvider : IEmailProvider
{
    private readonly IGmailSendService _gmailSendService;
    private readonly ILogger<GmailEmailProvider> _logger;

    public GmailEmailProvider(IGmailSendService gmailSendService, ILogger<GmailEmailProvider> logger)
    {
        _gmailSendService = gmailSendService;
        _logger = logger;
    }

    public string ProviderName => "Gmail";

    public async Task<EmailDeliveryResult> SendEmailAsync(Guid userId, EmailMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var (messageId, threadId) = await _gmailSendService.SendWithAttachmentAsync(
                userId, request.To, request.Subject, request.Body, request.CvPdfUrl, request.CvTitle, request.Attachments);

            return new EmailDeliveryResult
            {
                Success = true,
                MessageId = messageId,
                ThreadId = threadId
            };
        }
        catch (GmailReauthRequiredException ex)
        {
            _logger.LogWarning(ex, "Gmail reauthorization required for user {UserId}", userId);
            return new EmailDeliveryResult
            {
                Success = false,
                RequiresReauth = true,
                ErrorMessage = "Gmail connection expired or permissions revoked. Please reconnect your account."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver email via Gmail for user {UserId}", userId);
            return new EmailDeliveryResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
