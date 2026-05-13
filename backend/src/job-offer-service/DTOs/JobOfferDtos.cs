namespace JobOfferService.DTOs;

// ─── RESPONSES (OUTPUTS) ──────────────────────────────────────────────
public record JobOfferDetailDto(
    Guid Id,
    Guid UserId,
    string EnterpriseName,
    string? EnterpriseDescription,
    string JobRole,
    string RawDescription,
    int? RequiredExperienceYears,
    string? SeniorityLevel,
    string? EmploymentType,
    string? Location,
    string? LocationType,
    string? EducationRequirements,
    string? SourceUrl,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    
    // Nested collections for the detailed view
    List<JobSkillDto> Skills,
    List<JobResponsibilityDto> Responsibilities,
    List<JobBenefitDto> Benefits
);

// Child DTOs needed for the detailed view
public record JobSkillDto(
    Guid Id, 
    string Name, 
    string Type, 
    bool IsMandatory
);

public record JobResponsibilityDto(
    Guid Id, 
    string Description
);

public record JobBenefitDto(
    Guid Id, 
    string Description
);
public record JobOfferResponseDto(
    Guid Id,
    Guid UserId,
    string EnterpriseName,
    string? EnterpriseDescription,
    string JobRole,
    string RawDescription,
    int? RequiredExperienceYears,
    string? SeniorityLevel,
    string? EmploymentType,
    string? Location,
    string? LocationType,
    string? EducationRequirements,
    string? SourceUrl,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // Nested lists are optional so we don't over-fetch data if we don't need it
    List<JobSkillDto>? Skills = null,
    List<JobResponsibilityDto>? Responsibilities = null,
    List<JobBenefitDto>? Benefits = null
);

public record JobOfferSummaryDto(
    Guid Id,
    Guid UserId,
    string EnterpriseName,
    string JobRole,
    string? Location,
    string Status,
    DateTime CreatedAt
);

public record JobOfferListDto(
    List<JobOfferSummaryDto> Items,
    int Total,
    int Page,
    int PageSize
);

public record JobOfferStatisticsDto(
    int Total,
    int Draft,
    int Open,
    int Closed,
    int Archived
);


// ─── REQUESTS (INPUTS) ────────────────────────────────────────────────

// 1. What the user sends when they paste a link or raw text into the UI
public record SubmitJobOfferDto(
    Guid UserId,
    string? RawText,
    string? SourceUrl
);

// 2. What the AI Agent sends back after parsing the text
public record ExtractedJobDto(
    string EnterpriseName,
    string? EnterpriseDescription,
    string JobRole,
    string RawDescription,
    List<string> Responsibilities,
    List<string> RequiredSkills,
    List<string> SoftSkills,
    int? RequiredExperienceYears,
    string? SeniorityLevel,
    string? EmploymentType,
    string? Location,
    string? LocationType,
    string? EducationRequirements,
    List<string> Benefits,
    string? SourceUrl
);



// 3. For partial updates by the user (if they want to manually edit a typo)
public record UpdateJobOfferDto(
    string? EnterpriseName,
    string? JobRole,
    string? Location,
    string? EmploymentType,
    int? RequiredExperienceYears
);

// 4. For updating the state of the job offer
public record UpdateJobStatusDto(
    string Status
);