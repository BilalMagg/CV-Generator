using CV_Generator.Services;
using CV_Generator.Services.Email;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CV_Generator.Tests.Email;

public class EmailProviderTests
{
    [Fact]
    public async Task GmailEmailProvider_SendEmailAsync_CallsGmailSendService()
    {
        // Arrange
        var gmailSendService = Substitute.For<IGmailSendService>();
        var logger = Substitute.For<ILogger<GmailEmailProvider>>();
        var provider = new GmailEmailProvider(gmailSendService, logger);

        var userId = Guid.NewGuid();
        var request = new EmailMessageRequest
        {
            To = "candidate@example.com",
            Subject = "Job Application",
            Body = "<p>Please find attached my CV</p>"
        };

        gmailSendService.SendWithAttachmentAsync(
            userId,
            request.To,
            request.Subject,
            request.Body,
            request.CvPdfUrl,
            request.CvTitle,
            request.Attachments)
            .Returns(("msg-123", "thread-456"));

        // Act
        var result = await provider.SendEmailAsync(userId, request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("msg-123", result.MessageId);
        Assert.Equal("thread-456", result.ThreadId);
        Assert.Equal("Gmail", provider.ProviderName);
    }

    [Fact]
    public async Task GmailEmailProvider_WhenGmailSendFails_ReturnsFailureResultWithoutThrowing()
    {
        // Arrange
        var gmailSendService = Substitute.For<IGmailSendService>();
        var logger = Substitute.For<ILogger<GmailEmailProvider>>();
        var provider = new GmailEmailProvider(gmailSendService, logger);

        var userId = Guid.NewGuid();
        var request = new EmailMessageRequest
        {
            To = "to@example.com",
            Subject = "Subject",
            Body = "Body"
        };

        gmailSendService.SendWithAttachmentAsync(
            userId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<CV_Generator.Dto.EmailAttachmentDto>?>())
            .Returns<Task<(string?, string?)>>(x => throw new InvalidOperationException("Gmail token expired"));

        // Act
        var result = await provider.SendEmailAsync(userId, request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Gmail token expired", result.ErrorMessage);
    }
}
