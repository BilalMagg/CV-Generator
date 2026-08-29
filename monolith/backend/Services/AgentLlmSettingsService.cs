using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

public class AgentLlmSettingsService : IAgentLlmSettingsService
{
    private readonly AppDbContext _db;

    /// Synthetic agents that aren't rows in the `agents` catalog table but can
    /// still carry a per-agent LLM override (e.g. BIME, the conversational agent).
    public const string BimeAgentId = "bime";

    public AgentLlmSettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AgentLlmSettingsDto> GetAllAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        var uid = userId ?? Guid.Empty;
        var agents = await _db.Agents
            .AsNoTracking()
            .OrderBy(a => a.SortOrder)
            .ToListAsync(cancellationToken);
        var saved = await _db.AgentLlmSettings
            .AsNoTracking()
            .Where(s => s.UserId == uid)
            .ToListAsync(cancellationToken);

        var byAgent = saved.ToDictionary(s => s.AgentId, s => s);

        return new AgentLlmSettingsDto
        {
            Agents = agents.Select(a => byAgent.TryGetValue(a.AgentId, out var row)
                ? new AgentLlmSettingMapDto
                {
                    AgentId = a.AgentId,
                    Name = a.Name,
                    Provider = row.Provider,
                    Model = row.Model,
                }
                : new AgentLlmSettingMapDto
                {
                    AgentId = a.AgentId,
                    Name = a.Name,
                }).ToList()
        };
    }

    public async Task<AgentLlmSettingDto?> GetAsync(Guid? userId, string agentId, CancellationToken cancellationToken = default)
    {
        var uid = userId ?? Guid.Empty;
        var row = await _db.AgentLlmSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == uid && s.AgentId == agentId, cancellationToken);
        if (row == null) return null;

        return new AgentLlmSettingDto
        {
            AgentId = row.AgentId,
            Provider = row.Provider,
            Model = row.Model,
            UpdatedAt = row.UpdatedAt,
        };
    }

    public async Task<AgentLlmSettingDto> SetAsync(Guid? userId, string agentId, string provider, string model, CancellationToken cancellationToken = default)
    {
        if (agentId != BimeAgentId)
        {
            var known = await _db.Agents.AnyAsync(a => a.AgentId == agentId, cancellationToken);
            if (!known)
                throw new KeyNotFoundException($"Unknown agent '{agentId}'");
        }

        var uid = userId ?? Guid.Empty;
        var row = await _db.AgentLlmSettings
            .FirstOrDefaultAsync(s => s.UserId == uid && s.AgentId == agentId, cancellationToken);

        if (row == null)
        {
            row = new AgentLlmSetting
            {
                Id = Guid.NewGuid(),
                UserId = uid,
                AgentId = agentId,
                Provider = provider,
                Model = model,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.AgentLlmSettings.Add(row);
        }
        else
        {
            row.Provider = provider;
            row.Model = model;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new AgentLlmSettingDto
        {
            AgentId = row.AgentId,
            Provider = row.Provider,
            Model = row.Model,
            UpdatedAt = row.UpdatedAt,
        };
    }

    public async Task<(string? Provider, string? Model)> GetProviderModelAsync(Guid? userId, string agentId, CancellationToken cancellationToken = default)
    {
        var uid = userId ?? Guid.Empty;
        var row = await _db.AgentLlmSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == uid && s.AgentId == agentId, cancellationToken);
        return row == null ? (null, null) : (row.Provider, row.Model);
    }
}