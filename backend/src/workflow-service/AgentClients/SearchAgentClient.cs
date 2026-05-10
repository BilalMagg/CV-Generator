using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ISearchAgentClient
{
    Task<SearchOutput?> MatchAsync(SearchInput input);
    Task<bool> CheckHealthAsync();
}

public class SearchAgentClient : ISearchAgentClient
{
    private readonly HttpClient _client;

    public SearchAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<SearchOutput?> MatchAsync(SearchInput input)
    {
        var response = await _client.PostAsJsonAsync("match", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchOutput>();
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
