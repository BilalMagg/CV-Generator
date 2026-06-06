using System.Text.Json;
using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface IJobExtractorClient
{
    Task<ExtractorOutput?> ExtractAsync(ExtractorInput input, CancellationToken cancellationToken = default);
    Task<ExtractionFullResult?> ExtractFullAsync(JobExtractionRequest request, CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
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

    public async Task<ExtractorOutput?> ExtractAsync(ExtractorInput input, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("extract", input, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExtractorOutput>(cancellationToken: cancellationToken);
    }

    public async Task<ExtractionFullResult?> ExtractFullAsync(JobExtractionRequest request, CancellationToken cancellationToken = default)
    {
        var agentInput = new JobExtractorAgentRequest
        {
            JobDescription = request.Text,
            Url = request.Url,
            JobOfferId = request.JobOfferId,
            Language = request.Language,
        };

        var response = await _client.PostAsJsonAsync("extract", agentInput, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
            throw new HttpRequestException(
                $"Job extractor returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<ExtractionFullResult>(SnakeCaseOptions, cancellationToken: cancellationToken);
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
