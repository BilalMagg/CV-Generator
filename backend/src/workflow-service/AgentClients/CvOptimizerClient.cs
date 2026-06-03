using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ICvOptimizerClient
{
    Task<OptimizerOutput?> OptimizeAsync(OptimizerInput input, CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public class CvOptimizerClient : ICvOptimizerClient
{
    private readonly HttpClient _client;

    public CvOptimizerClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<OptimizerOutput?> OptimizeAsync(OptimizerInput input, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("optimize", input, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OptimizerOutput>(cancellationToken: cancellationToken);
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
