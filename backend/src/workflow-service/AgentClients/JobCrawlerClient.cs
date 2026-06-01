namespace WorkflowService.AgentClients;

public interface IJobCrawlerClient
{
    Task<bool> CheckHealthAsync();
}

public class JobCrawlerClient : IJobCrawlerClient
{
    private readonly HttpClient _client;

    public JobCrawlerClient(HttpClient client)
    {
        _client = client;
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
