namespace CvService.DTOs;

public record CvDto(
    Guid Id,
    Guid UserId,
    string Title,
    string TemplateId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsActive,
    List<CvVersionDto>? Versions = null
);

public record CvVersionDto(
    Guid Id,
    Guid CvId,
    int VersionNumber,
    string Label,
    string? FileUrl,
    string? PdfUrl,
    string ContentJson,
    DateTime CreatedAt
);

public record CvSectionDto(
    Guid Id,
    Guid VersionId,
    string SectionType,
    int DisplayOrder,
    string ContentJson,
    DateTime UpdatedAt
);
