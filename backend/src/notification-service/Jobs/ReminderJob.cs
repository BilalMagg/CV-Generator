using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Jobs;

public class ReminderJob
{
    private readonly NotificationDbContext _db;
    private readonly INotificationService _notificationSvc;
    private readonly ILogger<ReminderJob> _logger;

    public ReminderJob(NotificationDbContext db, INotificationService notificationSvc,
        ILogger<ReminderJob> logger)
    {
        _db = db;
        _notificationSvc = notificationSvc;
        _logger = logger;
    }

    /// <summary>
    /// Called daily by Hangfire — checks for applications with no response
    /// and fires reminders at 7 and 14 days.
    /// NOTE: This job queries the notification history to know which applications
    /// already had reminders sent, to avoid duplicates.
    /// The actual application data comes from the application-tracking-service
    /// (in Phase 2 you'll call it via gRPC; for now inject it or use a shared DB view).
    /// </summary>
    public async Task CheckAndSendRemindersAsync()
    {
        _logger.LogInformation("Running application reminder job at {Time}", DateTime.UtcNow);

        // In microservices phase: call application-tracking-service via gRPC
        // to get list of pending applications per user.
        // For now this is a placeholder showing the logic clearly.

        // Example shape of what you'd get from application-tracking-service:
        // { UserId, Email, FirstName, Company, Position, DaysSinceApplied }

        // var pendingApplications = await _appTrackingGrpcClient.GetPendingAsync();

        // foreach (var app in pendingApplications)
        // {
        //     if (app.DaysSinceApplied == 7 || app.DaysSinceApplied == 14)
        //     {
        //         bool alreadySent = _db.Notifications.Any(n =>
        //             n.UserId == app.UserId &&
        //             n.Type == (app.DaysSinceApplied == 7
        //                 ? NotificationType.ApplicationNoResponseOneWeek
        //                 : NotificationType.ApplicationNoResponseTwoWeeks));
        //
        //         if (!alreadySent)
        //             await _notificationSvc.SendNoResponseReminderAsync(...);
        //     }
        // }

        _logger.LogInformation("Reminder job completed");
    }
}