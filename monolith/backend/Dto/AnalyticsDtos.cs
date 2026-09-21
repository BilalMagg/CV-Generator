namespace CV_Generator.Dto;

/// <summary>Rich, single-call analytics payload powering the rewritten Analytics page.</summary>
public record AnalyticsSummaryDto(
    ApplicationStatisticsDto Statistics,
    double? AverageResponseTimeDays,
    int DistinctCompanies,
    List<MonthlyTrendDto> MonthlyTrends,
    List<DailyTrendDto> DailyTrends,
    Dictionary<string, int> PriorityCounts,
    Dictionary<string, int> OriginCounts,
    Dictionary<string, int> ChannelCounts,
    List<FunnelStageDto> Funnel,
    List<TopCompanyDto> TopCompanies,
    EmailStatsDto Email,
    ContactCoverageDto ContactCoverage,
    CompanyDistributionDto CompanyDistribution,
    CareerInventoryDto CareerInventory,
    ToolingUsageDto ToolingUsage,
    List<ResponseTimeBucketDto> ResponseTimeHistogram
);

public record FunnelStageDto(string Stage, int Count);

public record TopCompanyDto(string Name, int Count, DateTime? LastAppliedAt);

public record EmailStatsDto(int EmailsSent, int EmailsFailed, double SuccessRate, int ActiveSchedules);

public record ContactCoverageDto(
    int Total,
    int WithEmail,
    int WithPhone,
    int WithLinkedin,
    int WithMobile,
    int WithFax,
    double EmailPct,
    double PhonePct,
    double LinkedinPct,
    double MobilePct,
    double FaxPct
);

public record CompanyDistributionDto(
    int Total,
    int WithWebsite,
    int WithCountry,
    Dictionary<string, int> CityCounts,
    Dictionary<string, int> CountryCounts,
    Dictionary<string, int> SectorCounts
);

public record CareerInventoryDto(
    int Experiences,
    int Projects,
    int Skills,
    int Educations,
    int Certifications,
    int Hackathons,
    int Languages,
    int Interests,
    int AcademicActivities,
    int DistinctTags
);

public record ToolingUsageDto(
    int JobExtractions,
    int TemplateRenders,
    int CvGenerations,
    int SavedToolItems,
    int Cvs,
    int CvVersions,
    int CvTemplates,
    int CoverLetters,
    int CoverLetterVersions,
    int UserImages,
    int SchedulesActive,
    int SchedulesTotal
);

public record ResponseTimeBucketDto(string Bucket, int Count);
