using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;
using CV_Generator.Services.AgentClients;

namespace CV_Generator.Services;

/// <summary>
/// Per-user LLM provider API keys, AES-encrypted at rest and pushed (plaintext,
/// server-to-server on localhost) to the agent sidecar, which consults them in
/// preference to its env keys. Deployments are single-tenant, so the sidecar
/// keeps one in-memory key map — no per-request threading needed.
/// </summary>
public interface IUserLlmKeyService
{
    Task<UserLlmKeysResponseDto> ListAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Saves (creates or replaces) the key for one provider and pushes it to the sidecar.</summary>
    Task<UserLlmKeyDto> SetAsync(Guid userId, string provider, string apiKey, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid userId, string provider, CancellationToken ct = default);
    /// <summary>Best-effort re-push of every stored key — heals the sidecar's in-memory map after a sidecar restart.</summary>
    Task SyncToSidecarAsync(Guid userId, CancellationToken ct = default);
}

public class UserLlmKeyService : IUserLlmKeyService
{
    /// <summary>Providers accepted — mirrors the sidecar's PROVIDER_ENV_MAP keys.</summary>
    public static readonly string[] AllowedProviders =
        ["omniroute", "openrouter", "groq", "openai", "google", "mistral"];

    private readonly AppDbContext _db;
    private readonly IAesEncryptionService _aes;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UserLlmKeyService> _logger;

    public UserLlmKeyService(AppDbContext db, IAesEncryptionService aes,
        IHttpClientFactory httpClientFactory, ILogger<UserLlmKeyService> logger)
    {
        _db = db;
        _aes = aes;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<UserLlmKeysResponseDto> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.UserLlmKeys.AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderBy(k => k.Provider)
            .ToListAsync(ct);
        return new UserLlmKeysResponseDto
        {
            Keys = rows.Select(row => ToDto(row, SafeDecrypt(row.KeyCipher))).ToList(),
        };
    }

    public async Task<UserLlmKeyDto> SetAsync(Guid userId, string provider, string apiKey, CancellationToken ct = default)
    {
        provider = provider.Trim().ToLowerInvariant();
        if (!AllowedProviders.Contains(provider))
            throw new ArgumentException($"Unknown provider '{provider}'");
        apiKey = apiKey.Trim();
        if (apiKey.Length < 8) throw new ArgumentException("API key looks too short");
        if (apiKey.Length > 1000) throw new ArgumentException("API key is unreasonably long");

        var row = await _db.UserLlmKeys.FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == provider, ct);
        if (row is null)
        {
            row = new UserLlmKey { UserId = userId, Provider = provider, KeyCipher = _aes.Encrypt(apiKey) };
            _db.UserLlmKeys.Add(row);
        }
        else
        {
            row.KeyCipher = _aes.Encrypt(apiKey);
            row.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);

        PushOne(provider, apiKey);
        _logger.LogInformation("LLM key for provider {Provider} saved for user {UserId} and pushed to sidecar", provider, userId);
        return ToDto(row, apiKey);
    }

    public async Task<bool> DeleteAsync(Guid userId, string provider, CancellationToken ct = default)
    {
        provider = provider.Trim().ToLowerInvariant();
        var row = await _db.UserLlmKeys.FirstOrDefaultAsync(k => k.UserId == userId && k.Provider == provider, ct);
        if (row is null) return false;
        _db.UserLlmKeys.Remove(row);
        await _db.SaveChangesAsync(ct);
        PushRemove(provider);
        return true;
    }

    public async Task SyncToSidecarAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.UserLlmKeys.AsNoTracking()
            .Where(k => k.UserId == userId)
            .ToListAsync(ct);
        foreach (var row in rows)
        {
            var plain = SafeDecrypt(row.KeyCipher);
            if (plain is not null) PushOne(row.Provider, plain, ct);
        }
    }

    private async void PushOne(string provider, string apiKey, CancellationToken ct = default)
    {
        try
        {
            var http = _httpClientFactory.CreateClient("agents");
            var resp = await http.PostAsJsonAsync("llm/keys", new { provider, api_key = apiKey }, ct);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Sidecar key push for {Provider} returned {Status}", provider, resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sidecar key push for {Provider} failed (sidecar may be down)", provider);
        }
    }

    private async void PushRemove(string provider, CancellationToken ct = default)
    {
        try
        {
            var http = _httpClientFactory.CreateClient("agents");
            var resp = await http.DeleteAsync($"llm/keys/{Uri.EscapeDataString(provider)}", ct);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Sidecar key removal for {Provider} returned {Status}", provider, resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sidecar key removal for {Provider} failed", provider);
        }
    }

    private string? SafeDecrypt(string cipher)
    {
        try { return _aes.Decrypt(cipher); }
        catch (Exception ex) { _logger.LogError(ex, "Could not decrypt an LLM key"); return null; }
    }

    /// <summary>A short non-reversible hint: first/last 4 chars with the middle hidden.</summary>
    private static string? Mask(string? plain)
    {
        if (string.IsNullOrEmpty(plain) || plain.Length < 12) return "•••";
        return $"{plain[..6]}…{plain[^4..]}";
    }

    private static UserLlmKeyDto ToDto(UserLlmKey row, string? plain)
    {
        return new UserLlmKeyDto
        {
            Provider = row.Provider,
            Configured = true,
            Masked = Mask(plain),
            UpdatedAt = row.UpdatedAt,
        };
    }
}
