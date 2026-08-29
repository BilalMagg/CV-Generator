using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Models;
using CV_Generator.Dto;

namespace CV_Generator.Services;

public class LlmSettingsService : ILlmSettingsService
{
    private readonly AppDbContext _db;

    public LlmSettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<LlmSettingsDto?> GetAsync(Guid? userId)
    {
        var uid = userId ?? Guid.Empty;
        var row = await _db.UserLlmSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == uid);
        if (row == null) return null;

        return new LlmSettingsDto
        {
            Provider = row.Provider,
            Model = row.Model,
            UpdatedAt = row.UpdatedAt,
        };
    }

    public async Task<LlmSettingsDto> SetAsync(Guid? userId, string provider, string model)
    {
        var uid = userId ?? Guid.Empty;
        var row = await _db.UserLlmSettings
            .FirstOrDefaultAsync(s => s.UserId == uid);

        if (row == null)
        {
            row = new UserLlmSettings
            {
                Id = Guid.NewGuid(),
                UserId = uid,
                Provider = provider,
                Model = model,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.UserLlmSettings.Add(row);
        }
        else
        {
            row.Provider = provider;
            row.Model = model;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return new LlmSettingsDto
        {
            Provider = row.Provider,
            Model = row.Model,
            UpdatedAt = row.UpdatedAt,
        };
    }
}
