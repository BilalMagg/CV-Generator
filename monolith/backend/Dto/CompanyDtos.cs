namespace CV_Generator.Dto;

public class CompanyDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? WebsiteUrl { get; set; }
    public string? Location { get; set; }
    public string Country { get; set; } = "Morocco";
    public string? LocationUrl { get; set; }
    public string? Note { get; set; }
    public int ApplicationsCount { get; set; }
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
    public string? Note { get; set; }
}

public class UpdateCompanyDto
{
    public string? Name { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Location { get; set; }
    public string? Country { get; set; }
    public string? LocationUrl { get; set; }
    public string? Note { get; set; }
}

public class CompanyListResponse
{
    public List<CompanyDto> Items { get; set; } = [];
    public int Total { get; set; }
}
