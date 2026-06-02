using System.Text.Json;
using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface IJobExtractorClient
{
    Task<ExtractorOutput?> ExtractAsync(ExtractorInput input);
    Task<ExtractionFullResult?> ExtractFullAsync(JobExtractionRequest request);
    Task<bool> CheckHealthAsync();
}

public class JobExtractorClient : IJobExtractorClient
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

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

    public async Task<ExtractionFullResult?> ExtractFullAsync(JobExtractionRequest request)
    {
        var agentInput = new JobExtractorAgentRequest
        {
            JobDescription = request.Text,
            Url = request.Url,
            JobOfferId = request.JobOfferId,
            Language = request.Language,
        };

        var response = await _client.PostAsJsonAsync("extract", agentInput);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Job extractor returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<ExtractionFullResult>(SnakeCaseOptions);
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
