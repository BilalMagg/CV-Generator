using FluentValidation;
using CvService.DTOs;
using CvService.Entities;
using CvService.Events;
using CvService.Repositories;

namespace CvService.Services;

public class CvServiceImpl : ICvService
{
    private readonly ICvRepository _cvRepo;
    private readonly IValidator<CreateCvDto> _createValidator;
    private readonly IKafkaPublisher _kafkaPublisher;
    private readonly ILogger<CvServiceImpl> _logger;

    public CvServiceImpl(
        ICvRepository cvRepo,
        IValidator<CreateCvDto> createValidator,
        IKafkaPublisher kafkaPublisher,
        ILogger<CvServiceImpl> logger)
    {
        _cvRepo = cvRepo;
        _createValidator = createValidator;
        _kafkaPublisher = kafkaPublisher;
        _logger = logger;
    }

    public async Task<List<CvDto>> GetAllAsync(string userId)
    {
        var cvs = await _cvRepo.GetAllByUserIdAsync(userId);
        return cvs.Select(c => MapToDto(c, includeVersions: false)).ToList();
    }

    public async Task<CvDto?> GetByIdAsync(Guid id, string userId)
    {
        var cv = await _cvRepo.GetByIdAsync(id);
        if (cv == null || cv.UserId != Guid.Parse(userId)) return null;
        return MapToDto(cv, includeVersions: true);
    }

    public async Task<CvDto> CreateAsync(CreateCvDto dto, string userId)
    {
        await _createValidator.ValidateAndThrowAsync(dto);

        var cv = new Cv
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(userId),
            Title = dto.Title,
            TemplateId = dto.TemplateId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _cvRepo.CreateAsync(cv);

        await PublishEvent(new CVCreatedEvent
        {
            CVId = cv.Id,
            UserId = cv.UserId,
            Title = cv.Title,
            TemplateId = cv.TemplateId
        });

        return MapToDto(cv, includeVersions: false);
    }

    public async Task<CvDto?> UpdateAsync(Guid id, UpdateCvDto dto, string userId)
    {
        var cv = await _cvRepo.GetByIdAsync(id);
        if (cv == null || cv.UserId != Guid.Parse(userId)) return null;

        if (dto.Title != null) cv.Title = dto.Title;
        if (dto.TemplateId != null) cv.TemplateId = dto.TemplateId;
        if (dto.IsActive.HasValue) cv.IsActive = dto.IsActive.Value;

        await _cvRepo.UpdateAsync(cv);

        await PublishEvent(new CVUpdateEvent
        {
            CVId = cv.Id,
            UserId = cv.UserId
        });

        return MapToDto(cv, includeVersions: true);
    }

    public async Task<bool> DeleteAsync(Guid id, string userId)
    {
        var cv = await _cvRepo.GetByIdAsync(id);
        if (cv == null || cv.UserId != Guid.Parse(userId)) return false;

        var deleted = await _cvRepo.DeleteAsync(id);
        if (deleted)
        {
            await PublishEvent(new CVDeletedEvent
            {
                CVId = cv.Id,
                UserId = cv.UserId
            });
        }

        return deleted;
    }

    private static CvDto MapToDto(Cv cv, bool includeVersions) => new(
        cv.Id, cv.UserId, cv.Title, cv.TemplateId, cv.CreatedAt, cv.UpdatedAt, cv.IsActive,
        includeVersions
            ? cv.Versions
                .OrderBy(v => v.VersionNumber)
                .Select(v => new CvVersionDto(
                    v.Id, v.CvId, v.VersionNumber, v.Label, v.FileUrl, v.PdfUrl, v.ContentJson, v.CreatedAt))
                .ToList()
            : null
    );

    private async Task PublishEvent(CVEvent evt)
    {
        var topic = evt switch
        {
            CVCreatedEvent => "cv.created",
            CVUpdateEvent => "cv.updated",
            CVDeletedEvent => "cv.deleted",
            _ => throw new ArgumentException("Unknown event type")
        };

        await _kafkaPublisher.PublishAsync(evt, topic);
    }
}
