namespace ApplicationService.DTOs;

public record ApplicationResponseDto(
    Guid Id,
    Guid CandidateId,
    Guid? CvVersionId,
    Guid? JobOfferId,
    string CompanyName,
    string PositionTitle,
    string? OfferSource,
    string Status,
    DateTime? AppliedAt,
    DateTime UpdatedAt,
    string? Notes,
    string Origin = "MANUAL",
    List<StatusHistoryDto>? History = null,
    List<AttemptResponseDto>? Attempts = null
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
    string? Notes,
    string Origin = "MANUAL",
    string Status = "APPLIED",
    bool AllowDuplicate = false
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

public record DuplicateCheckRequestDto(
    string CompanyName,
    string PositionTitle,
    Guid? JobOfferId = null,
    Guid? ExcludeApplicationId = null
);

public record DuplicateMatchDto(
    Guid Id,
    string CompanyName,
    string PositionTitle,
    string Status,
    DateTime? AppliedAt,
    DateTime UpdatedAt,
    bool SameJobOffer
);

public record DuplicateCheckResponseDto(
    bool HasDuplicates,
    List<DuplicateMatchDto> Matches
);

public record CreateAttemptDto(
    string Channel,
    string InitiatedBy = "USER",
    string Status = "DRAFT",
    string? Subject = null,
    string? Body = null,
    string? RecipientName = null,
    string? RecipientContact = null,
    string? ChannelMetadataJson = null,
    Guid? CvVersionId = null,
    DateTime? SentAt = null,
    string? FailureReason = null
);

public record UpdateAttemptDto(
    string? Status = null,
    string? Subject = null,
    string? Body = null,
    string? RecipientName = null,
    string? RecipientContact = null,
    string? ChannelMetadataJson = null,
    Guid? CvVersionId = null,
    DateTime? SentAt = null,
    string? FailureReason = null
);

public record AttemptResponseDto(
    Guid Id,
    Guid ApplicationId,
    int AttemptNumber,
    string Channel,
    string InitiatedBy,
    string Status,
    string? Subject,
    string? Body,
    string? RecipientName,
    string? RecipientContact,
    string? ChannelMetadataJson,
    Guid? CvVersionId,
    DateTime? SentAt,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ApplicationStatisticsDto(
    int Total,
    int Saved,
    int Applied,
    int Screening,
    int Interview,
    int Offer,
    int Accepted,
    int Rejected,
    int Withdrawn
);

public record MonthlyTrendDto(
    int Year,
    int Month,
    int Saved,
    int Applied,
    int Screening,
    int Interview,
    int Offer,
    int Accepted,
    int Rejected,
    int Withdrawn
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
