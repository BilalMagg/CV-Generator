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
    DateTime AppliedAt,
    DateTime UpdatedAt,
    string? Notes,
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

public record ApplicationListDto(
    List<ApplicationResponseDto> Items,
    int Total,
    int Page,
    int PageSize
);