namespace WorkflowService.AgentClients;

public interface IJobCrawlerClient
{
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public class JobCrawlerClient : IJobCrawlerClient
{
    private readonly HttpClient _client;

    public JobCrawlerClient(HttpClient client)
    {
        _client = client;
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
