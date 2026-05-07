namespace CvService.DTOs;

public record CreateCvDto(
    string Title,
    string TemplateId
);

public record UpdateCvDto(
    string? Title,
    string? TemplateId,
    bool? IsActive
);

public record CreateCvVersionDto(
    string Label,
    string? ContentJson
);

public record UpdateSectionDto(
    string SectionType,
    int DisplayOrder,
    string ContentJson
);
