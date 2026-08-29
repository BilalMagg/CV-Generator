using System.Text.Json;
using CV_Generator.Dto;

namespace CV_Generator.Services.AgentClients;

public interface IJobExtractorClient
{
    Task<ExtractorOutput?> ExtractAsync(ExtractorInput input, CancellationToken cancellationToken = default);
    Task<ExtractionFullResult?> ExtractFullAsync(JobExtractionRequest request, CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public class JobExtractorClient : IJobExtractorClient
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public JobExtractorClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<ExtractorOutput?> ExtractAsync(ExtractorInput input, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("job", input, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
            throw new HttpRequestException(
                $"Job extractor returned {(int)response.StatusCode}: {errorBody}");
        }
        var full = await response.Content.ReadFromJsonAsync<ExtractionFullResult>(SnakeCaseOptions, cancellationToken: cancellationToken);
        return full == null ? null : MapToOutput(full);
    }

    private static int? RoundYears(double? years) =>
        years.HasValue ? (int)Math.Round(years.Value) : null;

    private static ExtractorOutput MapToOutput(ExtractionFullResult full)
    {
        return new ExtractorOutput
        {
            EnterpriseName = full.EnterpriseName,
            EnterpriseDescription = full.EnterpriseDescription,
            EnterpriseLogoUrl = full.EnterpriseLogoUrl,
            JobRole = full.JobRole,
            RawDescription = full.RawDescription,
            RequiredSkills = full.RequiredSkills,
            SoftSkills = full.SoftSkills,
            Location = full.Location,
            SalaryRange = full.SalaryRange,
            Currency = full.Currency,
            EducationRequirements = full.EducationRequirements,
            Benefits = full.Benefits,
            ApplicationDeadline = full.ApplicationDeadline,
            ContactEmail = full.ContactEmail,
            SourceUrl = full.SourceUrl,
            Languages = full.Languages,
            OverallConfidence = full.OverallConfidence,
            FieldConfidences = full.FieldConfidences,
            RequiredExperienceYears = RoundYears(full.RequiredExperienceYears),
            SeniorityLevel = full.SeniorityLevel,
            EmploymentType = full.EmploymentType,
            LocationType = full.LocationType,
            Responsibilities = full.Responsibilities,
            Certifications = full.Certifications,
            ExtractedSkills = full.RequiredSkills.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList(),
            Keywords = full.Responsibilities
                .Concat(full.RequiredSkills)
                .Concat(full.Certifications)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList(),
        };
    }

    public async Task<ExtractionFullResult?> ExtractFullAsync(JobExtractionRequest request, CancellationToken cancellationToken = default)
    {
        var agentInput = new JobExtractorAgentRequest
        {
            JobDescription = request.Text,
            Url = request.Url,
            JobOfferId = request.JobOfferId,
            Language = request.Language,
            Provider = request.Provider,
            Model = request.Model,
        };

        var response = await _client.PostAsJsonAsync("job", agentInput, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
            throw new HttpRequestException(
                $"Job extractor returned {(int)response.StatusCode}: {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<ExtractionFullResult>(SnakeCaseOptions, cancellationToken: cancellationToken);
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
