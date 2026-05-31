using ApplicationService.DTOs;
using ApplicationService.Entities;
using ApplicationService.Repositories;

namespace ApplicationService.Services;

public interface ICalendarConfigurationService
{
    Task<CalendarConfigurationDto?> GetAsync(Guid userId);
    Task<CalendarConfigurationDto> UpdateAsync(Guid userId, UpdateCalendarConfigurationDto dto);
}

public class CalendarConfigurationServiceImpl : ICalendarConfigurationService
{
    private readonly ICalendarConfigurationRepository _repo;

    public CalendarConfigurationServiceImpl(ICalendarConfigurationRepository repo)
    {
        _repo = repo;
    }

    public async Task<CalendarConfigurationDto?> GetAsync(Guid userId)
    {
        var config = await _repo.GetByUserIdAsync(userId);
        if (config == null) return null;

        return MapToDto(config);
    }

    public async Task<CalendarConfigurationDto> UpdateAsync(Guid userId, UpdateCalendarConfigurationDto dto)
    {
        var config = new ApplicationConfiguration
        {
            UserId = userId,
            ShowReminders = dto.ShowReminders,
            SelectedStatuses = dto.SelectedStatuses != null
                ? string.Join(",", dto.SelectedStatuses)
                : null
        };

        var saved = await _repo.UpsertAsync(config);
        return MapToDto(saved);
    }

    private static CalendarConfigurationDto MapToDto(ApplicationConfiguration config)
    {
        return new CalendarConfigurationDto(
            config.Id,
            config.UserId,
            config.ShowReminders,
            config.SelectedStatuses?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>()
        );
    }
}
