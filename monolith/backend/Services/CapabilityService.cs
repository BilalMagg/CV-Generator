using CV_Generator.Dto;

namespace CV_Generator.Services;

/// <summary>
/// Asks the agent sidecar what it can do (LaTeX engine, text→PDF) and caches the
/// answer briefly. Sidecar unreachable / probe errors degrade to "unavailable"
/// rather than throwing — capability reporting must never break a page load.
/// </summary>
public interface ICapabilityService
{
    Task<SystemCapabilitiesDto> GetAsync(CancellationToken cancellationToken = default);
}

public class CapabilityService : ICapabilityService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly HttpClient _client;
    private readonly ILogger<CapabilityService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private SystemCapabilitiesDto? _cached;
    private DateTimeOffset _cachedAt;

    public CapabilityService(HttpClient client, ILogger<CapabilityService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<SystemCapabilitiesDto> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
            return _cached;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
                return _cached;

            try
            {
                var response = await _client.GetAsync("capabilities", cancellationToken);
                response.EnsureSuccessStatusCode();
                var caps = await response.Content
                    .ReadFromJsonAsync<SystemCapabilitiesDto>(cancellationToken: cancellationToken);
                _cached = caps ?? new SystemCapabilitiesDto();
                _cached!.SidecarReachable = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Agent-sidecar capability probe failed; reporting degraded capabilities");
                _cached = new SystemCapabilitiesDto
                {
                    LatexEngineAvailable = false,
                    LatexEngineReason = "Agent sidecar unreachable",
                    TextPdfAvailable = false,
                    SidecarReachable = false,
                };
            }
            _cachedAt = DateTimeOffset.UtcNow;
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }
}
