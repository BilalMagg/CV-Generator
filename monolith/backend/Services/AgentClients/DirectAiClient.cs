using System.Text.Json;
using System.Text.Json.Serialization;
using CV_Generator.Dto;

namespace CV_Generator.Services.AgentClients;

public interface IDirectAiClient
{
    Task<DirectMessageResultDto?> GenerateMessageAsync(DirectMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<DirectChatResultDto?> ChatAsync(DirectChatRequestDto request, CancellationToken cancellationToken = default);
    Task<LinkedInResultDto?> GenerateLinkedInAsync(LinkedInRequestDto request, CancellationToken cancellationToken = default);
}

public class DirectAiClient : IDirectAiClient
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _client;

    public DirectAiClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<DirectMessageResultDto?> GenerateMessageAsync(DirectMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("message", request, SnakeCaseOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"direct-ai returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<DirectMessageResultDto>(SnakeCaseOptions, cancellationToken);
    }

    public async Task<DirectChatResultDto?> ChatAsync(DirectChatRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("chat", request, SnakeCaseOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"direct-ai returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<DirectChatResultDto>(SnakeCaseOptions, cancellationToken);
    }

    public async Task<LinkedInResultDto?> GenerateLinkedInAsync(LinkedInRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("linkedin", request, SnakeCaseOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"direct-ai returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<LinkedInResultDto>(SnakeCaseOptions, cancellationToken);
    }
}
