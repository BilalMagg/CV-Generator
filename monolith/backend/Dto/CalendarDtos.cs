namespace CV_Generator.Dto;

public record CalendarEventDto(
    string Date,
    string Type,
    string Title,
    Guid ApplicationId,
    string CompanyName,
    string PositionTitle
);

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
