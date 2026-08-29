using CV_Generator.Dto;

namespace CV_Generator.Services;

public interface IAgentLlmSettingsService
{
    /// Cross-catalog view: every known agent + the user's saved provider/model (or null).
    Task<AgentLlmSettingsDto> GetAllAsync(Guid? userId, CancellationToken cancellationToken = default);

    Task<AgentLlmSettingDto?> GetAsync(Guid? userId, string agentId, CancellationToken cancellationToken = default);

    Task<AgentLlmSettingDto> SetAsync(Guid? userId, string agentId, string provider, string model, CancellationToken cancellationToken = default);

    /// Resolves the per-agent provider/model for a pipeline step (nulls when unset).
    Task<(string? Provider, string? Model)> GetProviderModelAsync(Guid? userId, string agentId, CancellationToken cancellationToken = default);
}