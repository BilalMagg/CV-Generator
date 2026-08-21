using CV_Generator.Dto;

namespace CV_Generator.Services;

public interface IReminderService
{
    Task<Guid> CreateReminderAsync(CreateReminderDto dto);
    Task<List<ReminderResultDto>> GetUserRemindersAsync(Guid userId);
    Task CancelReminderAsync(Guid reminderId);

    Task ProcessDueRemindersAsync();
}
