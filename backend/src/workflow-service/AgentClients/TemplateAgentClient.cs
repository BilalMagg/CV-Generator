using System.Net.Http.Json;
using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ITemplateAgentClient
{
    Task<RenderedCV?> RenderAsync(TemplateInput input, CancellationToken cancellationToken = default);
    Task<List<TemplateDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public class TemplateAgentClient : ITemplateAgentClient
{
    private readonly HttpClient _client;

    private static readonly List<TemplateDefinition> _fallbackTemplates =
    [
        new() { Id = "default",   Name = "Default",    Type = "latex", Description = "Clean general-purpose layout" },
        new() { Id = "modern",    Name = "Modern",     Type = "latex", Description = "Contemporary two-column design" },
        new() { Id = "minimal",   Name = "Minimal",    Type = "html",  Description = "Simple, ATS-friendly single column" },
        new() { Id = "executive", Name = "Executive",  Type = "latex", Description = "Polished executive-level format" },
        new() { Id = "academic",  Name = "Academic",   Type = "latex", Description = "Research and academic CV format" },
    ];

    public TemplateAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<RenderedCV?> RenderAsync(TemplateInput input, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("render", input, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RenderedCV>(cancellationToken: cancellationToken);
    }

    public async Task<List<TemplateDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetAsync("templates", cancellationToken);
            if (!response.IsSuccessStatusCode) return _fallbackTemplates;
            var result = await response.Content.ReadFromJsonAsync<List<TemplateDefinition>>(cancellationToken: cancellationToken);
            return result?.Count > 0 ? result : _fallbackTemplates;
        }
        catch
        {
            return _fallbackTemplates;
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
