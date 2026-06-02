using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface IReminderService
{
    Task<Guid> CreateReminderAsync(CreateReminderDto dto);
    Task<List<ReminderResultDto>> GetUserRemindersAsync(Guid userId);
    Task CancelReminderAsync(Guid reminderId);

    /// <summary>
    /// Called by Hangfire every minute — finds due reminders and sends emails.
    /// </summary>
    Task ProcessDueRemindersAsync();
}
