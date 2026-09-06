using System.Text;
using System.Text.Json;
using CV_Generator.Dto;

namespace CV_Generator.Services.AgentClients;

public interface IAutofillClient
{
    /// <summary>Extract structured field values from a pasted description blob. Returns null on failure.</summary>
    Task<AutofillResultDto?> ExtractAsync(AutofillRequestDto request, CancellationToken cancellationToken = default);
}

public class AutofillClient : IAutofillClient
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public AutofillClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<AutofillResultDto?> ExtractAsync(AutofillRequestDto request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            entity_type = request.EntityType,
            text = request.Text,
            fields = request.Fields.Select(f => new
            {
                name = f.Name,
                label = f.Label,
                type = f.Type,
                options = f.Options,
                help = f.Help
            }).ToList(),
            provider = request.Provider,
            model = request.Model
        };

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("extract", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            var body = await response.Content.ReadFromJsonAsync<AutofillResponseDto>(SnakeCaseOptions, cancellationToken: cancellationToken);
            if (body?.Values == null)
                return null;
            return new AutofillResultDto { Values = body.Values };
        }
        catch
        {
            return null;
        }
    }
}

public class AutofillResponseDto
{
    public Dictionary<string, JsonElement>? Values { get; set; }
}
