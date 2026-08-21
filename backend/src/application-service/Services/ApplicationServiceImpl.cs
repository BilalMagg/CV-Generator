using ApplicationService.DTOs;
using ApplicationService.Entities;
using ApplicationService.Events;
using ApplicationService.Repositories;

namespace ApplicationService.Services;

public interface IApplicationService
{
    Task<ApplicationListDto> GetAllAsync(Guid? candidateId, int page, int pageSize, string[]? statuses = null, string? search = null, DateTime? appliedFrom = null, DateTime? appliedTo = null, DateTime? updatedFrom = null, DateTime? updatedTo = null);
    Task<ApplicationResponseDto?> GetByIdAsync(Guid id);
    Task<DuplicateCheckResponseDto> CheckDuplicatesAsync(Guid candidateId, DuplicateCheckRequestDto dto);
    Task<ApplicationResponseDto> CreateAsync(CreateApplicationDto dto, string? userId);
    Task<ApplicationResponseDto?> UpdateStatusAsync(Guid id, UpdateStatusDto dto, string? userId);
    Task<ApplicationResponseDto?> UpdateDetailsAsync(Guid id, UpdateApplicationDto dto, string? userId);
    Task<bool> DeleteAsync(Guid id);
    Task<ApplicationStatisticsDto> GetStatisticsAsync(Guid? candidateId);
    Task<StatisticsTrendsDto> GetTrendsAsync(Guid? candidateId);
    Task<bool?> ToggleSaveAsync(Guid id, string? userId);
    Task<ActivityFeedDto> GetActivityFeedAsync(Guid? candidateId, int limit = 50);
    Task<List<CalendarEventDto>> GetCalendarEventsAsync(Guid candidateId, DateTime from, DateTime to, string[]? statuses);
    Task<List<AttemptResponseDto>> GetAttemptsAsync(Guid applicationId);
    Task<AttemptResponseDto?> GetAttemptAsync(Guid applicationId, Guid attemptId);
    Task<AttemptResponseDto> CreateAttemptAsync(Guid applicationId, CreateAttemptDto dto, string? userId);
    Task<AttemptResponseDto?> UpdateAttemptAsync(Guid applicationId, Guid attemptId, UpdateAttemptDto dto);
}

public class ApplicationServiceImpl : IApplicationService
{
    private readonly IApplicationRepository _appRepo;
    private readonly IApplicationStatusHistoryRepository _historyRepo;
    private readonly IApplicationAttemptRepository _attemptRepo;
    private readonly IKafkaPublisher _kafkaPublisher;
    private readonly IUserGrpcClientService _userGrpc;
    private readonly ILogger<ApplicationServiceImpl> _logger;

    public ApplicationServiceImpl(
        IApplicationRepository appRepo,
        IApplicationStatusHistoryRepository historyRepo,
        IApplicationAttemptRepository attemptRepo,
        IKafkaPublisher kafkaPublisher,
        IUserGrpcClientService userGrpc,
        ILogger<ApplicationServiceImpl> logger)
    {
        _appRepo = appRepo;
        _historyRepo = historyRepo;
        _attemptRepo = attemptRepo;
        _kafkaPublisher = kafkaPublisher;
        _userGrpc = userGrpc;
        _logger = logger;
    }

    public async Task<ApplicationListDto> GetAllAsync(Guid? candidateId, int page, int pageSize, string[]? statuses = null, string? search = null, DateTime? appliedFrom = null, DateTime? appliedTo = null, DateTime? updatedFrom = null, DateTime? updatedTo = null)
    {
        var apps = await _appRepo.GetAllAsync(candidateId, page, pageSize, statuses, search, appliedFrom, appliedTo, updatedFrom, updatedTo);
        var total = await _appRepo.GetTotalCountAsync(candidateId, statuses, search, appliedFrom, appliedTo, updatedFrom, updatedTo);

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

    public async Task<DuplicateCheckResponseDto> CheckDuplicatesAsync(Guid candidateId, DuplicateCheckRequestDto dto)
    {
        var fingerprint = FingerprintHelper.Compute(dto.CompanyName, dto.PositionTitle);
        var matches = await _appRepo.FindDuplicatesAsync(candidateId, fingerprint, dto.JobOfferId, dto.ExcludeApplicationId);
        return MapDuplicateMatches(matches, dto.JobOfferId);
    }

    public async Task<ApplicationResponseDto> CreateAsync(CreateApplicationDto dto, string? userId)
    {
        if (!Enum.TryParse<ApplicationOrigin>(dto.Origin, true, out var origin))
            origin = ApplicationOrigin.MANUAL;

        var initialStatus = Enum.TryParse<ApplicationStatus>(dto.Status, true, out var parsedStatus)
            ? parsedStatus
            : ApplicationStatus.APPLIED;

        // Validate candidate exists via gRPC
        if (userId != null && Guid.TryParse(userId, out var userGuid))
        {
            var exists = await _userGrpc.UserExistsAsync(userGuid);
            if (!exists)
                _logger.LogWarning("Creating application for unknown user {UserId} — gRPC validation skipped if user-service unavailable", userId);
        }

        // Soft dedup: warn when an equivalent application already exists
        var fingerprint = FingerprintHelper.Compute(dto.CompanyName, dto.PositionTitle);
        var duplicates = await _appRepo.FindDuplicatesAsync(dto.CandidateId, fingerprint, dto.JobOfferId, excludeId: null);
        if (!dto.AllowDuplicate && duplicates.Count > 0)
        {
            _logger.LogInformation("Duplicate check hit for {Company}/{Position}: {Count} match(es)",
                dto.CompanyName, dto.PositionTitle, duplicates.Count);
            throw new DuplicateApplicationException(MapDuplicateMatches(duplicates, dto.JobOfferId));
        }

        var application = new Application
        {
            CandidateId = dto.CandidateId,
            CvVersionId = dto.CvVersionId,
            JobOfferId = dto.JobOfferId,
            CompanyName = dto.CompanyName,
            PositionTitle = dto.PositionTitle,
            OfferSource = dto.OfferSource,
            Origin = origin,
            Status = initialStatus,
            AppliedAt = initialStatus == ApplicationStatus.SAVED ? null : DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Notes = dto.Notes
        };

        var created = await _appRepo.CreateAsync(application);

        // Record initial history
        await _historyRepo.CreateAsync(new ApplicationStatusHistory
        {
            ApplicationId = created.Id,
            OldStatus = null,
            NewStatus = initialStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = userId,
            Comment = "Application created"
        });

        _logger.LogInformation("Application created {Id} ({Origin}, {Status}) by user {User}",
            created.Id, origin, initialStatus, userId);

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

        if (newStatus == ApplicationStatus.SAVED)
            app.AppliedAt = null;
        else if (newStatus == ApplicationStatus.APPLIED && oldStatus == ApplicationStatus.SAVED)
            app.AppliedAt = DateTime.UtcNow;

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
            stats.GetValueOrDefault(ApplicationStatus.SAVED, 0),
            stats.GetValueOrDefault(ApplicationStatus.APPLIED, 0),
            stats.GetValueOrDefault(ApplicationStatus.SCREENING, 0),
            stats.GetValueOrDefault(ApplicationStatus.INTERVIEW, 0),
            stats.GetValueOrDefault(ApplicationStatus.OFFER, 0),
            stats.GetValueOrDefault(ApplicationStatus.ACCEPTED, 0),
            stats.GetValueOrDefault(ApplicationStatus.REJECTED, 0),
            stats.GetValueOrDefault(ApplicationStatus.WITHDRAWN, 0)
        );
    }

    public async Task<StatisticsTrendsDto> GetTrendsAsync(Guid? candidateId)
    {
        var current = await GetStatisticsAsync(candidateId);
        var monthlyTrends = await _appRepo.GetMonthlyTrendsAsync(candidateId);
        var avgResponseTime = await _appRepo.GetAverageResponseTimeAsync(candidateId);

        return new StatisticsTrendsDto(current, monthlyTrends, avgResponseTime);
    }

    public async Task<bool?> ToggleSaveAsync(Guid id, string? userId)
    {
        var app = await _appRepo.GetByIdAsync(id);
        if (app == null) return null;

        var oldStatus = app.Status;
        var isSaved = app.Status == ApplicationStatus.SAVED;

        var newStatus = isSaved ? ApplicationStatus.APPLIED : ApplicationStatus.SAVED;

        if (newStatus == ApplicationStatus.SAVED)
            app.AppliedAt = null;
        else if (oldStatus == ApplicationStatus.SAVED && app.AppliedAt == null && !await _attemptRepo.HasSentAttemptAsync(id))
            app.AppliedAt = DateTime.UtcNow;

        app.Status = newStatus;
        app.UpdatedAt = DateTime.UtcNow;
        await _appRepo.UpdateAsync(app);

        await _historyRepo.CreateAsync(new ApplicationStatusHistory
        {
            ApplicationId = app.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = userId,
            Comment = isSaved ? "Removed from saved" : "Saved for later"
        });

        var evt = new ApplicationStatusUpdatedEvent
        {
            ApplicationId = app.Id,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            ChangedBy = userId,
            Comment = isSaved ? "Removed from saved" : "Saved for later"
        };
        await PublishEvent(evt);

        return !isSaved;
    }

    public async Task<List<CalendarEventDto>> GetCalendarEventsAsync(Guid candidateId, DateTime from, DateTime to, string[]? statuses)
    {
        return await _appRepo.GetCalendarEventsAsync(candidateId, from, to, statuses);
    }

    public async Task<ActivityFeedDto> GetActivityFeedAsync(Guid? candidateId, int limit = 50)
    {
        var items = await _appRepo.GetActivityFeedAsync(candidateId, limit);
        var total = await _appRepo.GetActivityFeedCountAsync(candidateId);
        return new ActivityFeedDto(items, total);
    }

    // ── Attempts ────────────────────────────────────────────────────────────────

    public async Task<List<AttemptResponseDto>> GetAttemptsAsync(Guid applicationId)
    {
        var attempts = await _attemptRepo.GetByApplicationAsync(applicationId);
        return attempts.Select(MapAttemptToDto).ToList();
    }

    public async Task<AttemptResponseDto?> GetAttemptAsync(Guid applicationId, Guid attemptId)
    {
        var attempt = await _attemptRepo.GetAsync(applicationId, attemptId);
        return attempt == null ? null : MapAttemptToDto(attempt);
    }

    public async Task<AttemptResponseDto> CreateAttemptAsync(Guid applicationId, CreateAttemptDto dto, string? userId)
    {
        var app = await _appRepo.GetByIdAsync(applicationId)
            ?? throw new KeyNotFoundException($"Application {applicationId} not found");

        Enum.TryParse<AttemptChannel>(dto.Channel, true, out var channel);
        Enum.TryParse<AttemptInitiatedBy>(dto.InitiatedBy, true, out var initiatedBy);
        Enum.TryParse<AttemptStatus>(dto.Status, true, out var status);

        var attempt = new ApplicationAttempt
        {
            ApplicationId = applicationId,
            AttemptNumber = await _attemptRepo.GetNextAttemptNumberAsync(applicationId),
            Channel = channel,
            InitiatedBy = initiatedBy,
            Status = status,
            Subject = dto.Subject,
            Body = dto.Body,
            RecipientName = dto.RecipientName,
            RecipientContact = dto.RecipientContact,
            ChannelMetadataJson = dto.ChannelMetadataJson,
            CvVersionId = dto.CvVersionId,
            SentAt = status == AttemptStatus.SENT ? dto.SentAt ?? DateTime.UtcNow : dto.SentAt,
            FailureReason = dto.FailureReason
        };

        var created = await _attemptRepo.CreateAsync(attempt);

        if (created.Status == AttemptStatus.SENT)
            await ApplySentSideEffectsAsync(app, created, userId);

        var evt = new ApplicationAttemptCreatedEvent
        {
            AttemptId = created.Id,
            ApplicationId = created.ApplicationId,
            AttemptNumber = created.AttemptNumber,
            Channel = created.Channel.ToString(),
            InitiatedBy = created.InitiatedBy.ToString(),
            Status = created.Status.ToString()
        };
        await PublishEvent(evt);

        return MapAttemptToDto(created);
    }

    public async Task<AttemptResponseDto?> UpdateAttemptAsync(Guid applicationId, Guid attemptId, UpdateAttemptDto dto)
    {
        var attempt = await _attemptRepo.GetAsync(applicationId, attemptId);
        if (attempt == null) return null;

        var oldStatus = attempt.Status;

        if (dto.Status != null) attempt.Status = Enum.Parse<AttemptStatus>(dto.Status.ToUpperInvariant());
        if (dto.Subject != null) attempt.Subject = dto.Subject;
        if (dto.Body != null) attempt.Body = dto.Body;
        if (dto.RecipientName != null) attempt.RecipientName = dto.RecipientName;
        if (dto.RecipientContact != null) attempt.RecipientContact = dto.RecipientContact;
        if (dto.ChannelMetadataJson != null) attempt.ChannelMetadataJson = dto.ChannelMetadataJson;
        if (dto.CvVersionId.HasValue) attempt.CvVersionId = dto.CvVersionId.Value;
        if (dto.FailureReason != null) attempt.FailureReason = dto.FailureReason;
        if (dto.SentAt.HasValue) attempt.SentAt = dto.SentAt;

        if (oldStatus != AttemptStatus.SENT && attempt.Status == AttemptStatus.SENT && attempt.SentAt == null)
            attempt.SentAt = DateTime.UtcNow;

        var updated = await _attemptRepo.UpdateAsync(attempt);

        if (oldStatus != AttemptStatus.SENT && updated.Status == AttemptStatus.SENT)
        {
            var app = await _appRepo.GetByIdAsync(applicationId);
            if (app != null)
                await ApplySentSideEffectsAsync(app, updated, userId: null);
        }

        var evt = new ApplicationAttemptUpdatedEvent
        {
            AttemptId = updated.Id,
            ApplicationId = updated.ApplicationId,
            AttemptNumber = updated.AttemptNumber,
            OldStatus = oldStatus.ToString(),
            NewStatus = updated.Status.ToString()
        };
        await PublishEvent(evt);

        return MapAttemptToDto(updated);
    }

    /// <summary>
    /// Side effects of a sent attempt: first real send moves SAVED → APPLIED,
    /// sets AppliedAt if unknown, and logs a feed entry so re-applies show up in the timeline.
    /// </summary>
    private async Task ApplySentSideEffectsAsync(Application app, ApplicationAttempt attempt, string? userId)
    {
        var comment = $"Attempt #{attempt.AttemptNumber} sent via {attempt.Channel}";

        if (app.AppliedAt == null || app.AppliedAt > attempt.SentAt)
            app.AppliedAt = attempt.SentAt;

        if (app.Status == ApplicationStatus.SAVED)
        {
            var oldStatus = app.Status;
            app.Status = ApplicationStatus.APPLIED;
            app.UpdatedAt = DateTime.UtcNow;
            await _appRepo.UpdateAsync(app);

            await _historyRepo.CreateAsync(new ApplicationStatusHistory
            {
                ApplicationId = app.Id,
                OldStatus = oldStatus,
                NewStatus = ApplicationStatus.APPLIED,
                ChangedAt = attempt.SentAt ?? DateTime.UtcNow,
                ChangedBy = userId,
                Comment = comment
            });
        }
        else
        {
            app.UpdatedAt = DateTime.UtcNow;
            await _appRepo.UpdateAsync(app);

            // Self-transition entry keeps the activity feed aware of every send/re-apply.
            await _historyRepo.CreateAsync(new ApplicationStatusHistory
            {
                ApplicationId = app.Id,
                OldStatus = app.Status,
                NewStatus = app.Status,
                ChangedAt = attempt.SentAt ?? DateTime.UtcNow,
                ChangedBy = userId,
                Comment = comment
            });
        }
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private DuplicateCheckResponseDto MapDuplicateMatches(List<Application> matches, Guid? jobOfferId)
        => new(
            matches.Count > 0,
            matches.Select(a => new DuplicateMatchDto(
                a.Id,
                a.CompanyName,
                a.PositionTitle,
                a.Status.ToString(),
                a.AppliedAt,
                a.UpdatedAt,
                jobOfferId.HasValue && a.JobOfferId == jobOfferId.Value
            )).ToList()
        );

    private static ApplicationResponseDto MapToDto(Application a) => new(
        a.Id, a.CandidateId, a.CvVersionId, a.JobOfferId,
        a.CompanyName, a.PositionTitle, a.OfferSource,
        a.Status.ToString(), a.AppliedAt, a.UpdatedAt, a.Notes, a.Origin.ToString()
    );

    private static ApplicationResponseDto MapToDtoWithHistory(Application a) => new(
        a.Id, a.CandidateId, a.CvVersionId, a.JobOfferId,
        a.CompanyName, a.PositionTitle, a.OfferSource,
        a.Status.ToString(), a.AppliedAt, a.UpdatedAt, a.Notes, a.Origin.ToString(),
        a.StatusHistory?.Select(h => new StatusHistoryDto(
            h.Id, h.OldStatus?.ToString(), h.NewStatus.ToString(),
            h.ChangedAt, h.ChangedBy, h.Comment
        )).ToList(),
        a.Attempts?.Select(t => MapAttemptToDto(t)).ToList()
    );

    private static AttemptResponseDto MapAttemptToDto(ApplicationAttempt t) => new(
        t.Id, t.ApplicationId, t.AttemptNumber,
        t.Channel.ToString(), t.InitiatedBy.ToString(), t.Status.ToString(),
        t.Subject, t.Body, t.RecipientName, t.RecipientContact,
        t.ChannelMetadataJson, t.CvVersionId,
        t.SentAt, t.FailureReason, t.CreatedAt, t.UpdatedAt
    );

    private async Task PublishEvent(ApplicationEvent evt)
    {
        var topic = evt switch
        {
            ApplicationCreatedEvent => "application.created",
            ApplicationStatusUpdatedEvent => "application.status.updated",
            ApplicationUpdatedEvent => "application.updated",
            ApplicationDeletedEvent => "application.deleted",
            ApplicationAttemptCreatedEvent => "application.attempt.created",
            ApplicationAttemptUpdatedEvent => "application.attempt.updated",
            _ => throw new ArgumentException("Unknown event type")
        };

        await _kafkaPublisher.PublishAsync(evt, topic);
    }
}
