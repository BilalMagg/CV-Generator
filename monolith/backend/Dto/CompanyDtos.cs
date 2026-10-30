namespace CV_Generator.Dto;

public class CompanySocialLinkDto
{
    public string Key { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class CompanyDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? WebsiteUrl { get; set; }
    public string? Location { get; set; }
    public string Country { get; set; } = "Morocco";
    public string? LocationUrl { get; set; }
    public string? Region { get; set; }
    public string? Sector { get; set; }
    public int? FoundedYear { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Size { get; set; }
    public string? Specialties { get; set; }
    public Guid? LogoImageId { get; set; }
    public string? LogoUrl { get; set; }
    public string? Note { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public List<string> Emails { get; set; } = [];
    public List<string> Phones { get; set; } = [];
    public List<CompanySocialLinkDto> SocialLinks { get; set; } = [];
    public List<string> CompanyFacts { get; set; } = [];
    public string? ResearchSource { get; set; }
    public string? ResearchLink { get; set; }
    public DateTime? ResearchUpdatedAt { get; set; }
    public int ApplicationsCount { get; set; }
    public int ContactsCount { get; set; }
    public DateTime? LastAppliedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateCompanyDto
{
    public string Name { get; set; } = string.Empty;
    public string? WebsiteUrl { get; set; }
    public string? Location { get; set; }
    public string? Country { get; set; }
    public string? LocationUrl { get; set; }
    public string? Region { get; set; }
    public string? Sector { get; set; }
    public int? FoundedYear { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Size { get; set; }
    public string? Specialties { get; set; }
    public Guid? LogoImageId { get; set; }
    public string? LogoUrl { get; set; }
    public string? Note { get; set; }
    public string? Description { get; set; }
}

public class UpdateCompanyDto
{
    public string? Name { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Location { get; set; }
    public string? Country { get; set; }
    public string? LocationUrl { get; set; }
    public string? Region { get; set; }
    public string? Sector { get; set; }
    public int? FoundedYear { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Size { get; set; }
    public string? Specialties { get; set; }
    public Guid? LogoImageId { get; set; }
    public string? LogoUrl { get; set; }
    public string? Note { get; set; }
    public string? Description { get; set; }
}

public class CompanyListResponse
{
    public List<CompanyDto> Items { get; set; } = [];
    public int Total { get; set; }
}

public class CompanyCredentialDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string? Label { get; set; }
    public string? Url { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpsertCompanyCredentialDto
{
    public string? Label { get; set; }
    public string? Url { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Serialization helpers for the company web-research fields (stored as jsonb).</summary>
public static class CompanyResearchJson
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<CompanySocialLinkDto> ParseSocialLinks(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<CompanySocialLinkDto>>(raw, Options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static List<string> ParseList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw, Options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string? SerializeList(IEnumerable<string>? values)
    {
        var list = values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
        return list is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(list) : null;
    }

    public static string? SerializeSocialLinks(IEnumerable<CompanySocialLinkDto>? links)
    {
        var list = links?
            .Where(l => !string.IsNullOrWhiteSpace(l.Key) && !string.IsNullOrWhiteSpace(l.Url))
            .Select(l => new CompanySocialLinkDto { Key = l.Key.Trim().ToLower(), Url = l.Url.Trim() })
            .ToList();
        return list is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(list) : null;
    }
}

public class DuplicateApplicationPairDto
{
    public ApplicationResponseDto ApplicationA { get; set; } = null!;
    public ApplicationResponseDto ApplicationB { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
}

public class DuplicateContactPairDto
{
    public ContactDto ContactA { get; set; } = null!;
    public ContactDto ContactB { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
}

public class CompanyMergePreviewDto
{
    public CompanyDto CompanyA { get; set; } = null!;
    public CompanyDto CompanyB { get; set; } = null!;

    public List<ApplicationResponseDto> ApplicationsA { get; set; } = [];
    public List<ApplicationResponseDto> ApplicationsB { get; set; } = [];
    public List<DuplicateApplicationPairDto> DuplicateApplications { get; set; } = [];

    public List<ContactDto> ContactsA { get; set; } = [];
    public List<ContactDto> ContactsB { get; set; } = [];
    public List<DuplicateContactPairDto> DuplicateContacts { get; set; } = [];

    public List<CompanyCredentialDto> CredentialsA { get; set; } = [];
    public List<CompanyCredentialDto> CredentialsB { get; set; } = [];

    public DateTime OldestCreatedAt { get; set; }
}

public class MergeContactResolutionDto
{
    public string Mode { get; set; } = "KeepBoth"; // "KeepA", "KeepB", "KeepBoth"
    public Guid ContactAId { get; set; }
    public Guid ContactBId { get; set; }
}

public class MergeApplicationResolutionDto
{
    public string Mode { get; set; } = "KeepBoth"; // "Merge", "KeepBoth"
    public Guid PrimaryApplicationId { get; set; }
    public Guid SecondaryApplicationId { get; set; }
}

/// <summary>A suggested near-duplicate pair (e.g. "Inetum" / "Inetum Maroc" created by extraction).</summary>
public class CompanyMergeCandidateDto
{
    public Guid IdA { get; set; }
    public Guid IdB { get; set; }
    public string NameA { get; set; } = string.Empty;
    public string NameB { get; set; } = string.Empty;
    /// <summary>Why the pair was suggested: "same-base" | "prefix-match".</summary>
    public string Reason { get; set; } = string.Empty;
    public int AppsA { get; set; }
    public int AppsB { get; set; }
    public DateTime CreatedA { get; set; }
    public DateTime CreatedB { get; set; }
}

public class CompanyMergeRequestDto
{
    public Guid SourceCompanyId { get; set; }
    public Guid TargetCompanyId { get; set; }
    public string FinalName { get; set; } = string.Empty;

    public string? WebsiteUrl { get; set; }
    public string? Location { get; set; }
    public string? Country { get; set; }
    public string? LocationUrl { get; set; }
    public string? Region { get; set; }
    public string? Sector { get; set; }
    public int? FoundedYear { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Size { get; set; }
    public string? Specialties { get; set; }
    public Guid? LogoImageId { get; set; }
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public string? Note { get; set; }

    public List<Guid>? KeepCredentialIds { get; set; }
    public List<MergeContactResolutionDto>? ContactResolutions { get; set; }
    public List<MergeApplicationResolutionDto>? ApplicationResolutions { get; set; }
}
