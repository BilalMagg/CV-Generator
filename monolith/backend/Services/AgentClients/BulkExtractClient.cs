using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CV_Generator.Dto;

namespace CV_Generator.Services.AgentClients;

public interface IBulkExtractClient
{
    /// <summary>LLM-extract bulk-apply rows from a pasted blob. Returns null on failure.</summary>
    Task<List<BulkExtractRowDto>?> ExtractAsync(BulkExtractRequestDto request, CancellationToken cancellationToken = default);
}

public class BulkExtractClient : IBulkExtractClient
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public BulkExtractClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<List<BulkExtractRowDto>?> ExtractAsync(BulkExtractRequestDto request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            text = request.Text,
            language = request.Language,
            provider = request.Provider,
            model = request.Model
        };

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("rows", content, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadFromJsonAsync<BulkExtractResponseDto>(SnakeCaseOptions, cancellationToken: cancellationToken);
            if (body?.Rows == null) return null;

            return body.Rows
                .Where(r => !string.IsNullOrWhiteSpace(r.CompanyName))
                .Select(r => new BulkExtractRowDto
                {
                    CompanyName = r.CompanyName.Trim(),
                    Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim(),
                    Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim(),
                    ContactName = string.IsNullOrWhiteSpace(r.ContactName) ? null : r.ContactName.Trim(),
                    Specialties = r.Specialties?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? []
                })
                .ToList();
        }
        catch
        {
            return null;
        }
    }
}

public class BulkExtractResponseDto
{
    [JsonPropertyName("rows")]
    public List<BulkExtractRowResponseDto>? Rows { get; set; }
}

public class BulkExtractRowResponseDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Email { get; set; }
    public string? ContactName { get; set; }
    public List<string>? Specialties { get; set; }
}
