namespace NotificationService.Infrastructure.Messaging;

public static class KafkaTopics
{
    public const string UserCreated = "user.created";
    public const string CvGenerated = "cv.generated";
    public const string ApplicationCreated = "application.created";
    public const string ApplicationStatusChanged = "application.status.updated";
    public const string EmailSendRequested = "email.send.requested";
    public const string EmailSent = "email.sent";
    public const string EmailFailed = "email.failed";
}