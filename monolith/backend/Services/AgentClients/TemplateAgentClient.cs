using CV_Generator.Dto;

namespace CV_Generator.Services.AgentClients;

public interface ITemplateAgentClient
{
    Task<RenderedCV?> RenderAsync(TemplateInput input, CancellationToken cancellationToken = default);
    Task<PdfOutput?> CompilePdfAsync(PdfInput input, CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public class TemplateAgentClient : ITemplateAgentClient
{
    private readonly HttpClient _client;

    public TemplateAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<RenderedCV?> RenderAsync(TemplateInput input, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("render", input, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
            throw new HttpRequestException(
                $"Template agent (render) returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<RenderedCV>(cancellationToken: cancellationToken);
    }

    public async Task<PdfOutput?> CompilePdfAsync(PdfInput input, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("pdf", input, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
            throw new HttpRequestException(
                $"Template agent (PDF) returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<PdfOutput>(cancellationToken: cancellationToken);
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
