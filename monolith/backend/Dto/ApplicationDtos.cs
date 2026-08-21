namespace CV_Generator.Dto;

public record ApplicationResponseDto(
    Guid Id,
    Guid CandidateId,
    Guid? CvVersionId,
    Guid? JobOfferId,
    string CompanyName,
    string PositionTitle,
    string? OfferSource,
    string Status,
    DateTime AppliedAt,
    DateTime UpdatedAt,
    string? Notes,
    bool IsSaved = false,
    List<StatusHistoryDto>? History = null
);

public record StatusHistoryDto(
    Guid Id,
    string? OldStatus,
    string NewStatus,
    DateTime ChangedAt,
    string? ChangedBy,
    string? Comment
);

public record CreateApplicationDto(
    Guid CandidateId,
    Guid? CvVersionId,
    Guid? JobOfferId,
    string CompanyName,
    string PositionTitle,
    string? OfferSource,
    string? Notes
);

public record UpdateStatusDto(
    string Status,
    string? Comment
);

public record UpdateApplicationDto(
    string? CompanyName,
    string? PositionTitle,
    string? OfferSource,
    string? Notes
);

public record ApplicationStatisticsDto(
    int Total,
    int Pending,
    int Reviewed,
    int Interview,
    int Accepted,
    int Rejected,
    int Cancelled
);

public record MonthlyTrendDto(
    int Year,
    int Month,
    int Pending,
    int Reviewed,
    int Interview,
    int Accepted,
    int Rejected,
    int Cancelled
);

public record StatisticsTrendsDto(
    ApplicationStatisticsDto Current,
    List<MonthlyTrendDto> MonthlyTrends,
    double? AverageResponseTimeDays
);

public record SeedApplicationsDto(
    int Count = 500,
    int MonthsBack = 12
);

public record SeedResultDto(
    int ApplicationsCreated,
    int StatusHistoryCreated,
    Guid CandidateId
);

public record ActivityItemDto(
    Guid ApplicationId,
    string CompanyName,
    string PositionTitle,
    string? OldStatus,
    string NewStatus,
    DateTime ChangedAt,
    string? Comment
);

public record ActivityFeedDto(
    List<ActivityItemDto> Items,
    int Total
);

public record ApplicationListDto(
    List<ApplicationResponseDto> Items,
    int Total,
    int Page,
    int PageSize
);
