using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface INotificationService
{
    Task SendWelcomeAsync(Guid userId, string email, string firstName);
    Task SendCvGeneratedAsync(Guid userId, string email, string firstName, string cvDownloadUrl);
    Task SendApplicationCreatedAsync(Guid userId, string email, string firstName, string company, string position);
    Task SendApplicationStatusChangedAsync(Guid userId, string email, string firstName, string company, string position, string newStatus);
    Task SendNoResponseReminderAsync(Guid userId, string email, string firstName, string company, string position, int daysSinceApplied);
    Task SendWeeklyDigestAsync(Guid userId, string email, string firstName, int totalApplications, int responses, int cvsGenerated);
    Task<List<NotificationResultDto>> GetUserNotificationsAsync(Guid userId);
    Task MarkAsReadAsync(Guid notificationId);
}