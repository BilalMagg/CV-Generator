using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ITemplateAgentClient
{
    Task<RenderedCV?> RenderAsync(TemplateInput input, CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public class TemplateAgentClient : ITemplateAgentClient
{
    private readonly HttpClient _client;

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
