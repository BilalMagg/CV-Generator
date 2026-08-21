using CV_Generator.Dto;

namespace CV_Generator.Services.AgentClients;

public interface IContactAgentClient
{
    Task<ContactOutput?> DeliverAsync(ContactInput input, CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public class ContactAgentClient : IContactAgentClient
{
    private readonly HttpClient _client;

    public ContactAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<ContactOutput?> DeliverAsync(ContactInput input, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("deliver", input, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContactOutput>(cancellationToken: cancellationToken);
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
