using System.Text.Json;
using CVGenerator.Shared;

namespace WorkflowService.AgentClients;

public class TriggerCrawlRequest
{
    public Guid UserId { get; set; }
    public string Keyword { get; set; } = "";
    public string Location { get; set; } = "";
    public int ResultLimit { get; set; } = 20;
}

public class TriggerCrawlResponse
{
    public Guid SearchId { get; set; }
    public string Keyword { get; set; } = "";
    public string Location { get; set; } = "";
    public int ResultLimit { get; set; }
}

public interface IJobOfferServiceClient
{
    Task<TriggerCrawlResponse> TriggerCrawlAsync(TriggerCrawlRequest request);
}

public class JobOfferServiceClient : IJobOfferServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<JobOfferServiceClient> _logger;

    public JobOfferServiceClient(HttpClient http, ILogger<JobOfferServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<TriggerCrawlResponse> TriggerCrawlAsync(TriggerCrawlRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/job-offers/crawl", request);
        response.EnsureSuccessStatusCode();
        var wrapper = await response.Content.ReadFromJsonAsync<ApiResponse<TriggerCrawlResponse>>();
        return wrapper?.Data ?? throw new Exception("Failed to trigger crawl via job-offer-service");
    }
}
