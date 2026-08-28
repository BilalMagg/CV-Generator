namespace CV_Generator.Dto;

public class JobExtractionRequest
{
    public string? Text { get; set; }
    public string? Url { get; set; }
    public string? JobOfferId { get; set; }
    public string Language { get; set; } = "en";

    public string? Provider { get; set; }
    public string? Model { get; set; }
}

public class ExtractionResponse
{
    public Guid Id { get; set; }
    public ExtractionFullResult Output { get; set; } = new();
}

public class ExtractionFullResult
{
    public string? EnterpriseName { get; set; }
    public string? EnterpriseDescription { get; set; }
    public string? EnterpriseLogoUrl { get; set; }
    public string? JobRole { get; set; }
    public string? RawDescription { get; set; }
    public List<string> Responsibilities { get; set; } = new();
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> SoftSkills { get; set; } = new();
    public double? RequiredExperienceYears { get; set; }
    public string? SeniorityLevel { get; set; }
    public string? EmploymentType { get; set; }
    public string? Location { get; set; }
    public string? LocationType { get; set; }
    public string? SalaryRange { get; set; }
    public string? Currency { get; set; }
    public List<string> Certifications { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public string? EducationRequirements { get; set; }
    public List<string> Benefits { get; set; } = new();
    public string? ApplicationDeadline { get; set; }
    public string? ContactEmail { get; set; }
    public string? SourceUrl { get; set; }
    public Dictionary<string, double> FieldConfidences { get; set; } = new();
    public double OverallConfidence { get; set; }
}

public class ExtractionHistoryItemDto
{
    public Guid Id { get; set; }
    public string JobRole { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public double OverallConfidence { get; set; }
    public string SourceType { get; set; } = string.Empty;
}
