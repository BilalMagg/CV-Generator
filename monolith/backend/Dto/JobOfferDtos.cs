namespace CV_Generator.Dto;

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
    List<JobSkillDto> Skills,
    List<JobResponsibilityDto> Responsibilities,
    List<JobBenefitDto> Benefits
);

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

public record SubmitJobOfferDto(
    Guid UserId,
    string? RawText,
    string? SourceUrl
);

public record ExtractedJobDto(
    string EnterpriseName,
    string? EnterpriseDescription,
    string JobRole,
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
    string RawDescription = "",
    Guid? SearchId = null,
    string? Source = null,
    double? OverallConfidence = null
);

public record UpdateJobOfferDto(
    string? EnterpriseName,
    string? JobRole,
    string? Location,
    string? EmploymentType,
    int? RequiredExperienceYears
);

public record UpdateJobStatusDto(
    string Status
);

public record JobArrivedDto(
    Guid JobId,
    string? Title,
    string? Company,
    string? Location,
    string? Source,
    string? JobUrl,
    double? Confidence
);

public record SearchFinishedDto(
    Guid SearchId,
    int TotalProcessed
);

public record TriggerCrawlDto(
    Guid UserId,
    string Keyword,
    string Location,
    int ResultLimit = 20
);

public record TriggerCrawlResponseDto(
    Guid SearchId,
    string Keyword,
    string Location,
    int ResultLimit
);

public record CrawlHistoryDto(
    Guid SearchId,
    string Keyword,
    string? Location,
    string Status,
    int ExpectedCount,
    int ProcessedCount,
    DateTime CreatedAt
);

public record CrawlJobDto(
    Guid JobId,
    string Title,
    string Company,
    string? Location,
    string JobUrl,
    double Confidence
);

public record CrawlPollResponseDto(
    Guid SearchId,
    string Status,
    string Keyword,
    string? Location,
    int ExpectedCount,
    int ProcessedCount,
    List<CrawlJobDto> Jobs
);
