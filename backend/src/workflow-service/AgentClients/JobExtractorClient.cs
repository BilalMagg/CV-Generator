using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface IJobExtractorClient
{
    Task<ExtractorOutput?> ExtractAsync(ExtractorInput input);
    Task<bool> CheckHealthAsync();
}

public class JobExtractorClient : IJobExtractorClient
{
    private readonly HttpClient _client;

    public JobExtractorClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<ExtractorOutput?> ExtractAsync(ExtractorInput input)
    {
        var response = await _client.PostAsJsonAsync("extract", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExtractorOutput>();
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
