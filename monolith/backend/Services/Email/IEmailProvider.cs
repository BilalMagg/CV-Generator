namespace CV_Generator.Services.Email;

public class EmailMessageRequest
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public string? CvPdfUrl { get; set; }
    public string? CvTitle { get; set; }
    public IReadOnlyList<CV_Generator.Dto.EmailAttachmentDto>? Attachments { get; set; }
}

public class EmailDeliveryResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ThreadId { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RequiresReauth { get; set; }
}

public interface IEmailProvider
{
    string ProviderName { get; }
    Task<EmailDeliveryResult> SendEmailAsync(Guid userId, EmailMessageRequest request, CancellationToken ct = default);
}
