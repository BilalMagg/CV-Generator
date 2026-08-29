using System.Text.Json;
using System.Text.Json.Serialization;
using CV_Generator.Dto;

namespace CV_Generator.Services.AgentClients;

public interface IApplyPrepClient
{
    Task<ApplyPrepFormResult?> GenerateFormResponsesAsync(ApplyPrepFormRequest request, CancellationToken cancellationToken = default);
    Task<ApplyPrepMessageResult?> GenerateMessageAsync(ApplyPrepMessageRequest request, CancellationToken cancellationToken = default);
}

public class ApplyPrepClient : IApplyPrepClient
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _client;

    public ApplyPrepClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<ApplyPrepFormResult?> GenerateFormResponsesAsync(ApplyPrepFormRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("form-responses", request, SnakeCaseOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"apply-prep returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<ApplyPrepFormResult>(SnakeCaseOptions, cancellationToken);
    }

    public async Task<ApplyPrepMessageResult?> GenerateMessageAsync(ApplyPrepMessageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("message", request, SnakeCaseOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"apply-prep returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<ApplyPrepMessageResult>(SnakeCaseOptions, cancellationToken);
    }
}
