using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ITemplateAgentClient
{
    Task<RenderedCV?> RenderAsync(TemplateInput input);
    Task<bool> CheckHealthAsync();
}

public class TemplateAgentClient : ITemplateAgentClient
{
    private readonly HttpClient _client;

    public TemplateAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<RenderedCV?> RenderAsync(TemplateInput input)
    {
        var response = await _client.PostAsJsonAsync("render", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RenderedCV>();
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _client.GetAsync("health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
