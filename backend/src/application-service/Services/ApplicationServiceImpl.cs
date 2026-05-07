using ApplicationService.DTOs;
using ApplicationService.Entities;
using ApplicationService.Events;
using ApplicationService.Repositories;

namespace ApplicationService.Services;

public interface IApplicationService
{
    Task<ApplicationListDto> GetAllAsync(Guid? candidateId, int page, int pageSize);
    Task<ApplicationResponseDto?> GetByIdAsync(Guid id);
    Task<ApplicationResponseDto> CreateAsync(CreateApplicationDto dto, string? userId);
    Task<ApplicationResponseDto?> UpdateStatusAsync(Guid id, UpdateStatusDto dto, string? userId);
    Task<ApplicationResponseDto?> UpdateDetailsAsync(Guid id, UpdateApplicationDto dto, string? userId);
    Task<bool> DeleteAsync(Guid id);
    Task<ApplicationStatisticsDto> GetStatisticsAsync(Guid? candidateId);
}

public class ApplicationServiceImpl : IApplicationService
{
    private readonly IApplicationRepository _appRepo;
    private readonly IApplicationStatusHistoryRepository _historyRepo;
    private readonly IKafkaPublisher _kafkaPublisher;
    private readonly ILogger<ApplicationServiceImpl> _logger;

    public ApplicationServiceImpl(
        IApplicationRepository appRepo,
        IApplicationStatusHistoryRepository historyRepo,
        IKafkaPublisher kafkaPublisher,
        ILogger<ApplicationServiceImpl> logger)
    {
        _appRepo = appRepo;
        _historyRepo = historyRepo;
        _kafkaPublisher = kafkaPublisher;
        _logger = logger;
    }

    public async Task<ApplicationListDto> GetAllAsync(Guid? candidateId, int page, int pageSize)
    {
        var apps = await _appRepo.GetAllAsync(candidateId, page, pageSize);
        var total = await _appRepo.GetTotalCountAsync(candidateId);

        return new ApplicationListDto(
            apps.Select(MapToDto).ToList(),
            total,
            page,
            pageSize
        );
    }

    public async Task<ApplicationResponseDto?> GetByIdAsync(Guid id)
    {
        var app = await _appRepo.GetByIdWithHistoryAsync(id);
        return app == null ? null : MapToDtoWithHistory(app);
    }

    public async Task<ApplicationResponseDto> CreateAsync(CreateApplicationDto dto, string? userId)
    {
        var application = new Application
        {
            CandidateId = dto.CandidateId,
            CvVersionId = dto.CvVersionId,
            JobOfferId = dto.JobOfferId,
            CompanyName = dto.CompanyName,
            PositionTitle = dto.PositionTitle,
            OfferSource = dto.OfferSource,
            Status = ApplicationStatus.PENDING,
            AppliedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Notes = dto.Notes
        };

        var created = await _appRepo.CreateAsync(application);

        // Record initial history
        await _historyRepo.CreateAsync(new ApplicationStatusHistory
        {
            ApplicationId = created.Id,
            OldStatus = null,
            NewStatus = ApplicationStatus.PENDING,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = userId,
            Comment = "Application created"
        });

        _logger.LogInformation("Application created {Id} by user {User}", created.Id, userId);

        // Emit event (event-ready architecture)
        var evt = new ApplicationCreatedEvent
        {
            ApplicationId = created.Id,
            CandidateId = created.CandidateId,
            CompanyName = created.CompanyName,
            PositionTitle = created.PositionTitle,
            Status = created.Status.ToString()
        };

        await PublishEvent(evt);

        return MapToDto(created);
    }

    public async Task<ApplicationResponseDto?> UpdateStatusAsync(Guid id, UpdateStatusDto dto, string? userId)
    {
        var app = await _appRepo.GetByIdAsync(id);
        if (app == null) return null;

        var oldStatus = app.Status;
        var newStatus = Enum.Parse<ApplicationStatus>(dto.Status.ToUpperInvariant());

        app.Status = newStatus;
        app.UpdatedAt = DateTime.UtcNow;

        await _appRepo.UpdateAsync(app);

        // Record history
        await _historyRepo.CreateAsync(new ApplicationStatusHistory
        {
            ApplicationId = app.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = userId,
            Comment = dto.Comment
        });

        _logger.LogInformation("Application {Id} status updated from {Old} to {New} by {User}",
            id, oldStatus, newStatus, userId);

        var evt = new ApplicationStatusUpdatedEvent
        {
            ApplicationId = app.Id,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            ChangedBy = userId,
            Comment = dto.Comment
        };

        await PublishEvent(evt);

        var result = await _appRepo.GetByIdWithHistoryAsync(id);
        return result == null ? null : MapToDtoWithHistory(result);
    }

    public async Task<ApplicationResponseDto?> UpdateDetailsAsync(Guid id, UpdateApplicationDto dto, string? userId)
    {
        try
        {
            var updated = await _appRepo.UpdateDetailsAsync(id, dto);

            var evt = new ApplicationUpdatedEvent
            {
                ApplicationId = updated.Id,
                CandidateId = updated.CandidateId,
                CompanyName = updated.CompanyName,
                PositionTitle = updated.PositionTitle,
                UpdatedBy = userId
            };

            await PublishEvent(evt);

            var result = await _appRepo.GetByIdWithHistoryAsync(id);
            return result == null ? null : MapToDtoWithHistory(result);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var app = await _appRepo.GetByIdAsync(id);
        if (app == null) return false;

        var evt = new ApplicationDeletedEvent
        {
            ApplicationId = id,
            CandidateId = app.CandidateId
        };

        var deleted = await _appRepo.DeleteAsync(id);

        if (deleted)
        {
            _logger.LogInformation("Application {Id} deleted", id);
            await PublishEvent(evt);
        }

        return deleted;
    }

    public async Task<ApplicationStatisticsDto> GetStatisticsAsync(Guid? candidateId)
    {
        var stats = await _appRepo.GetStatisticsAsync(candidateId);

        return new ApplicationStatisticsDto(
            stats.Values.Sum(),
            stats.GetValueOrDefault(ApplicationStatus.PENDING, 0),
            stats.GetValueOrDefault(ApplicationStatus.REVIEWED, 0),
            stats.GetValueOrDefault(ApplicationStatus.INTERVIEW, 0),
            stats.GetValueOrDefault(ApplicationStatus.ACCEPTED, 0),
            stats.GetValueOrDefault(ApplicationStatus.REJECTED, 0),
            stats.GetValueOrDefault(ApplicationStatus.CANCELLED, 0)
        );
    }

    private static ApplicationResponseDto MapToDto(Application a) => new(
        a.Id, a.CandidateId, a.CvVersionId, a.JobOfferId,
        a.CompanyName, a.PositionTitle, a.OfferSource,
        a.Status.ToString(), a.AppliedAt, a.UpdatedAt, a.Notes
    );

    private static ApplicationResponseDto MapToDtoWithHistory(Application a) => new(
        a.Id, a.CandidateId, a.CvVersionId, a.JobOfferId,
        a.CompanyName, a.PositionTitle, a.OfferSource,
        a.Status.ToString(), a.AppliedAt, a.UpdatedAt, a.Notes,
        a.StatusHistory?.Select(h => new StatusHistoryDto(
            h.Id, h.OldStatus?.ToString(), h.NewStatus.ToString(),
            h.ChangedAt, h.ChangedBy, h.Comment
        )).ToList()
    );

    private async Task PublishEvent(ApplicationEvent evt)
    {
        var topic = evt switch
        {
            ApplicationCreatedEvent => "application.created",
            ApplicationStatusUpdatedEvent => "application.status.updated",
            ApplicationUpdatedEvent => "application.updated",
            ApplicationDeletedEvent => "application.deleted",
            _ => throw new ArgumentException("Unknown event type")
        };

        await _kafkaPublisher.PublishAsync(evt, topic);
    }
}