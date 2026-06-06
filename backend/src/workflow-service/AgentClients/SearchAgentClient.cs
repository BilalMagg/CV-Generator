using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ISearchAgentClient
{
    Task<SearchOutput?> MatchAsync(SearchInput input, CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public class SearchAgentClient : ISearchAgentClient
{
    private readonly HttpClient _client;

    public SearchAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<SearchOutput?> MatchAsync(SearchInput input, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("match", input, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchOutput>(cancellationToken: cancellationToken);
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
