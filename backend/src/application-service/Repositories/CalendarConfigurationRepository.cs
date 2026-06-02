using Microsoft.EntityFrameworkCore;
using ApplicationService.Entities;

namespace ApplicationService.Repositories;

public interface ICalendarConfigurationRepository
{
    Task<ApplicationConfiguration?> GetByUserIdAsync(Guid userId);
    Task<ApplicationConfiguration> UpsertAsync(ApplicationConfiguration config);
}

public class CalendarConfigurationRepository : ICalendarConfigurationRepository
{
    private readonly ApplicationDbContext _db;

    public CalendarConfigurationRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ApplicationConfiguration?> GetByUserIdAsync(Guid userId)
    {
        return await _db.ApplicationConfigurations
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<ApplicationConfiguration> UpsertAsync(ApplicationConfiguration config)
    {
        var existing = await _db.ApplicationConfigurations
            .FirstOrDefaultAsync(c => c.UserId == config.UserId);

        if (existing != null)
        {
            existing.ShowReminders = config.ShowReminders;
            existing.SelectedStatuses = config.SelectedStatuses;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.ApplicationConfigurations.Update(existing);
            await _db.SaveChangesAsync();
            return existing;
        }

        config.Id = Guid.NewGuid();
        config.UpdatedAt = DateTime.UtcNow;
        _db.ApplicationConfigurations.Add(config);
        await _db.SaveChangesAsync();
        return config;
    }
}
