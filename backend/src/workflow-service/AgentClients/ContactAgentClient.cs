using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface IContactAgentClient
{
    Task<ContactOutput?> DeliverAsync(ContactInput input, CancellationToken cancellationToken = default);
    Task<GenerateEmailResponse?> GenerateAsync(GenerateEmailRequest input, CancellationToken cancellationToken = default);
    Task<BulkGenerateContactResponse> GenerateBulkAsync(BulkGenerateContactRequest input, CancellationToken cancellationToken = default);
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

    public async Task<GenerateEmailResponse?> GenerateAsync(GenerateEmailRequest input, CancellationToken cancellationToken = default)
    {
        var agentRequest = new ContactAgentGenerateRequest
        {
            JobTitle = input.JobTitle,
            CompanyName = input.CompanyName,
            JobDescription = input.JobDescription,
            CoverLetterHint = input.CoverLetterHint,
            CandidateContext = input.CandidateContext,
        };
        var response = await _client.PostAsJsonAsync("generate", agentRequest, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        var agentResult = await response.Content.ReadFromJsonAsync<ContactAgentGenerateResponse>(cancellationToken: cancellationToken);
        if (agentResult == null) return null;
        return new GenerateEmailResponse { Subject = agentResult.Subject, Body = agentResult.Body };
    }

    public async Task<BulkGenerateContactResponse> GenerateBulkAsync(BulkGenerateContactRequest input, CancellationToken cancellationToken = default)
    {
        var tasks = input.Contacts.Select(async contact =>
        {
            var agentRequest = new ContactAgentGenerateRequest
            {
                JobTitle = input.JobTitle,
                CompanyName = input.CompanyName,
                JobDescription = input.JobDescription,
                CoverLetterHint = input.CoverLetterHint,
                CandidateContext = input.CandidateContext,
                RecipientName = contact.Name,
                RecipientCompany = contact.Company,
            };

            try
            {
                var response = await _client.PostAsJsonAsync("generate", agentRequest, cancellationToken: cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<ContactAgentGenerateResponse>(cancellationToken: cancellationToken);
                return new BulkGenerateContactResult
                {
                    ContactId = contact.Id,
                    ContactName = contact.Name,
                    ContactEmail = contact.Email,
                    Subject = result?.Subject,
                    Body = result?.Body,
                };
            }
            catch (Exception ex)
            {
                return new BulkGenerateContactResult
                {
                    ContactId = contact.Id,
                    ContactName = contact.Name,
                    ContactEmail = contact.Email,
                    Error = ex.Message,
                };
            }
        });

        var results = await Task.WhenAll(tasks);
        return new BulkGenerateContactResponse
        {
            Results = results.ToList(),
            Total = results.Length,
            Generated = results.Count(r => r.Error == null),
            Failed = results.Count(r => r.Error != null),
        };
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
