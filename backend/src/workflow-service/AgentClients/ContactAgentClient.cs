using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface IContactAgentClient
{
    Task<ContactOutput?> DeliverAsync(ContactInput input);
    Task<bool> CheckHealthAsync();
}

public class ContactAgentClient : IContactAgentClient
{
    private readonly HttpClient _client;

    public ContactAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<ContactOutput?> DeliverAsync(ContactInput input)
    {
        var response = await _client.PostAsJsonAsync("deliver", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContactOutput>();
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
