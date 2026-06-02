namespace ApplicationService.DTOs;

public record CalendarEventDto(
    string Date,
    string Type,
    string Title,
    Guid ApplicationId,
    string CompanyName,
    string PositionTitle
);
