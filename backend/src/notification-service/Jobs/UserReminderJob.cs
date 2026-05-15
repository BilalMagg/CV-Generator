using NotificationService.Application.Interfaces;

namespace NotificationService.Jobs;

public class UserReminderJob
{
    private readonly IReminderService _reminderSvc;
    private readonly ILogger<UserReminderJob> _logger;

    public UserReminderJob(IReminderService reminderSvc, ILogger<UserReminderJob> logger)
    {
        _reminderSvc = reminderSvc;
        _logger = logger;
    }

    /// <summary>
    /// Called every minute by Hangfire — processes all due reminders and sends emails.
    /// </summary>
    public async Task ProcessAsync()
    {
        _logger.LogDebug("UserReminderJob triggered at {Time}", DateTime.UtcNow);
        await _reminderSvc.ProcessDueRemindersAsync();
    }
}
