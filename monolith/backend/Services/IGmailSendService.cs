namespace CV_Generator.Services;

public interface IGmailSendService
{
    Task SendWithAttachmentAsync(Guid userId, string to, string subject, string body, string? cvPdfUrl = null);
}
