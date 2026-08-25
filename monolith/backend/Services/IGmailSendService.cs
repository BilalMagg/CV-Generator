using CV_Generator.Dto;

namespace CV_Generator.Services;

public interface IGmailSendService
{
    /// <summary>Sends via the user's Gmail and returns the Gmail message/thread ids.</summary>
    Task<(string? MessageId, string? ThreadId)> SendWithAttachmentAsync(
        Guid userId, string to, string subject, string body,
        string? cvPdfUrl = null, IReadOnlyList<EmailAttachmentDto>? attachments = null);
}
