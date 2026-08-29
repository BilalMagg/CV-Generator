using CV_Generator.Dto;

namespace CV_Generator.Services.AgentClients;

public interface ICategorizationClient
{
    Task<List<Guid>> CategorizeAsync(string scope, string text, List<CategoryCandidateDto> candidates, CancellationToken cancellationToken = default);
}

public class CategorizationClient : ICategorizationClient
{
    private readonly HttpClient _client;

    public CategorizationClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<List<Guid>> CategorizeAsync(string scope, string text, List<CategoryCandidateDto> candidates, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            scope,
            text,
            candidates = candidates.Select(c => new { id = c.Id.ToString(), name = c.Name, keywords = c.Keywords }).ToList()
        };
        try
        {
            var response = await _client.PostAsJsonAsync("", payload, cancellationToken: cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new List<Guid>();
            var body = await response.Content.ReadFromJsonAsync<CategorizeResponseDto>(cancellationToken: cancellationToken);
            if (body?.NodeIds == null)
                return new List<Guid>();
            return body.NodeIds
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();
        }
        catch
        {
            return new List<Guid>();
        }
    }
}

public class CategoryCandidateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
}

public class CategorizeResponseDto
{
    public List<string> NodeIds { get; set; } = new();
}
