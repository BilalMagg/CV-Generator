namespace CV_Generator.Dto;

/// <summary>Rich, single-call analytics payload powering the rewritten Analytics page.</summary>
public record AnalyticsSummaryDto(
    ApplicationStatisticsDto Statistics,
    double? AverageResponseTimeDays,
    int DistinctCompanies,
    List<MonthlyTrendDto> MonthlyTrends,
    Dictionary<string, int> PriorityCounts,
    Dictionary<string, int> OriginCounts,
    Dictionary<string, int> ChannelCounts,
    List<FunnelStageDto> Funnel,
    List<TopCompanyDto> TopCompanies,
    EmailStatsDto Email
);

public record FunnelStageDto(string Stage, int Count);

public record TopCompanyDto(string Name, int Count, DateTime? LastAppliedAt);

public record EmailStatsDto(int EmailsSent, int EmailsFailed, double SuccessRate, int ActiveSchedules);
