namespace ApplicationService.DTOs;

public record CalendarConfigurationDto(
    Guid Id,
    Guid UserId,
    bool ShowReminders,
    string[] SelectedStatuses
);

public record UpdateCalendarConfigurationDto(
    bool ShowReminders,
    string[] SelectedStatuses
);
