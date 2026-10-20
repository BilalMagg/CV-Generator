using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CV_Generator.Dto;

namespace CV_Generator.Services.AgentClients;

public interface IBulkExtractClient
{
    /// <summary>LLM-extract bulk-apply rows from a pasted blob. Returns null on failure.</summary>
    Task<List<BulkExtractRowDto>?> ExtractAsync(BulkExtractRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// LLM-pick a template per row from the offered candidates. Returns null on failure;
    /// otherwise a list aligned with the request rows (Guid.Empty = no match).
    /// </summary>
    Task<List<Guid>?> MapTemplatesAsync(TemplateMapRequestDto request, CancellationToken cancellationToken = default);
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

    public async Task<List<Guid>?> MapTemplatesAsync(TemplateMapRequestDto request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            rows = request.Rows.Select(r => new
            {
                company_name = r.CompanyName,
                location = r.Location,
                specialty = r.Specialty
            }),
            templates = request.Templates.Select(t => new
            {
                id = t.Id.ToString(),
                name = t.Name,
                subject_template = t.SubjectTemplate,
                body_template = t.BodyTemplate,
                intent = t.Intent
            }),
            provider = request.Provider,
            model = request.Model
        };

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("map-templates", content, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadFromJsonAsync<TemplateMapResponseDto>(SnakeCaseOptions, cancellationToken: cancellationToken);
            if (body?.TemplateIds == null) return null;

            // Align to the request: one id per row, Guid.Empty when blank/unparseable.
            var result = new List<Guid>(request.Rows.Count);
            for (var i = 0; i < request.Rows.Count; i++)
            {
                var raw = i < body.TemplateIds.Count ? body.TemplateIds[i] : null;
                result.Add(Guid.TryParse(raw, out var id) ? id : Guid.Empty);
            }
            return result;
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

public class TemplateMapResponseDto
{
    [JsonPropertyName("template_ids")]
    public List<string?>? TemplateIds { get; set; }
}
