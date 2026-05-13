using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ICvOptimizerClient
{
    Task<OptimizerOutput?> OptimizeAsync(OptimizerInput input);
    Task<bool> CheckHealthAsync();
}

public class CvOptimizerClient : ICvOptimizerClient
{
    private readonly HttpClient _client;

    public CvOptimizerClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<OptimizerOutput?> OptimizeAsync(OptimizerInput input)
    {
        var response = await _client.PostAsJsonAsync("optimize", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OptimizerOutput>();
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
