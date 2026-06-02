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

// 2. What the AI Agent sends back after parsing the text.
//    The 3 optional fields at the bottom are only populated by the crawler pipeline.
//    The existing /{id}/extracted HTTP endpoint ignores them (they default to null).
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
    string? SourceUrl,
    // ── Crawler-only fields (null when posted from the manual flow) ──────────────
    Guid? SearchId = null,
    string? Source = null,
    double? OverallConfidence = null
);



// 4. For partial updates by the user (if they want to manually edit a typo)
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

// ─── CRAWLER PIPELINE DTOs ────────────────────────────────────────────────

/// <summary>SignalR push payload — one per job as it arrives.</summary>
public record JobArrivedDto(
    Guid JobId,
    string? Title,
    string? Company,
    string? Location,
    string? Source,
    string? JobUrl,
    double? Confidence
);

/// <summary>SignalR event fired when ProcessedCount >= ExpectedCount.</summary>
public record SearchFinishedDto(
    Guid SearchId,
    int TotalProcessed
);

/// <summary>Trigger payload from the frontend to start a live crawl.</summary>
public record TriggerCrawlDto(
    Guid UserId,
    string Keyword,
    string Location,
    int ResultLimit = 20
);

/// <summary>
/// Returned immediately to the frontend after a crawl is triggered.
/// The frontend uses SearchId to join the SignalR group and receive real-time updates.
/// </summary>
public record TriggerCrawlResponseDto(
    Guid SearchId,
    string Keyword,
    string Location,
    int ResultLimit
);