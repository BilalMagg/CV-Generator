using Microsoft.EntityFrameworkCore;
using ApplicationService.DTOs;
using ApplicationService.Entities;

namespace ApplicationService.Repositories;

public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(Guid id);
    Task<Application?> GetByIdWithHistoryAsync(Guid id);
    Task<List<Application>> GetAllAsync(Guid? candidateId, int page, int pageSize, string[]? statuses = null, string? search = null, DateTime? appliedFrom = null, DateTime? appliedTo = null, DateTime? updatedFrom = null, DateTime? updatedTo = null);
    Task<int> GetTotalCountAsync(Guid? candidateId, string[]? statuses = null, string? search = null, DateTime? appliedFrom = null, DateTime? appliedTo = null, DateTime? updatedFrom = null, DateTime? updatedTo = null);
    Task<Application> CreateAsync(Application application);
    Task<Application> UpdateAsync(Application application);
    Task<Application> UpdateDetailsAsync(Guid id, UpdateApplicationDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<Dictionary<ApplicationStatus, int>> GetStatisticsAsync(Guid? candidateId);
    Task<List<MonthlyTrendDto>> GetMonthlyTrendsAsync(Guid? candidateId, int months = 12);
    Task<double?> GetAverageResponseTimeAsync(Guid? candidateId);
    Task<bool> ToggleSaveAsync(Guid id);
    Task<List<CalendarEventDto>> GetCalendarEventsAsync(Guid candidateId, DateTime from, DateTime to, string[]? statuses);
    Task<List<ActivityItemDto>> GetActivityFeedAsync(Guid? candidateId, int limit = 50);
    Task<int> GetActivityFeedCountAsync(Guid? candidateId);
}

public class ApplicationRepository : IApplicationRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ApplicationRepository> _logger;

    public ApplicationRepository(ApplicationDbContext db, ILogger<ApplicationRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Application?> GetByIdAsync(Guid id)
        => await _db.Applications.FindAsync(id);

    public async Task<Application?> GetByIdWithHistoryAsync(Guid id)
        => await _db.Applications
            .Include(a => a.StatusHistory.OrderByDescending(h => h.ChangedAt))
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<List<Application>> GetAllAsync(Guid? candidateId, int page, int pageSize, string[]? statuses = null, string? search = null, DateTime? appliedFrom = null, DateTime? appliedTo = null, DateTime? updatedFrom = null, DateTime? updatedTo = null)
    {
        var query = BuildFilteredQuery(candidateId, statuses, search, appliedFrom, appliedTo, updatedFrom, updatedTo);

        return await query
            .OrderByDescending(a => a.AppliedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(Guid? candidateId, string[]? statuses = null, string? search = null, DateTime? appliedFrom = null, DateTime? appliedTo = null, DateTime? updatedFrom = null, DateTime? updatedTo = null)
    {
        var query = BuildFilteredQuery(candidateId, statuses, search, appliedFrom, appliedTo, updatedFrom, updatedTo);
        return await query.CountAsync();
    }

    private IQueryable<Application> BuildFilteredQuery(Guid? candidateId, string[]? statuses, string? search, DateTime? appliedFrom, DateTime? appliedTo, DateTime? updatedFrom, DateTime? updatedTo)
    {
        var query = _db.Applications.AsQueryable();

        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);

        if (statuses is { Length: > 0 })
        {
            var parsed = statuses
                .Select(s => Enum.TryParse<ApplicationStatus>(s, true, out var st) ? st : (ApplicationStatus?)null)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToList();
            if (parsed.Count > 0)
                query = query.Where(a => parsed.Contains(a.Status));
        }

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a =>
                EF.Functions.ILike(a.CompanyName, $"%{search}%") ||
                EF.Functions.ILike(a.PositionTitle, $"%{search}%"));

        if (appliedFrom.HasValue)
            query = query.Where(a => a.AppliedAt >= appliedFrom.Value);
        if (appliedTo.HasValue)
            query = query.Where(a => a.AppliedAt <= appliedTo.Value);

        if (updatedFrom.HasValue)
            query = query.Where(a => a.UpdatedAt >= updatedFrom.Value);
        if (updatedTo.HasValue)
            query = query.Where(a => a.UpdatedAt <= updatedTo.Value);

        return query;
    }

    public async Task<Application> CreateAsync(Application application)
    {
        _db.Applications.Add(application);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created application {Id}", application.Id);
        return application;
    }

    public async Task<Application> UpdateAsync(Application application)
    {
        application.UpdatedAt = DateTime.UtcNow;
        _db.Applications.Update(application);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated application {Id}", application.Id);
        return application;
    }

    public async Task<Application> UpdateDetailsAsync(Guid id, UpdateApplicationDto dto)
    {
        var application = await _db.Applications.FindAsync(id);
        if (application == null) throw new KeyNotFoundException($"Application {id} not found");

        if (dto.CompanyName != null) application.CompanyName = dto.CompanyName;
        if (dto.PositionTitle != null) application.PositionTitle = dto.PositionTitle;
        if (dto.OfferSource != null) application.OfferSource = dto.OfferSource;
        if (dto.Notes != null) application.Notes = dto.Notes;

        application.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated application details {Id}", id);
        return application;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var application = await _db.Applications.FindAsync(id);
        if (application == null) return false;

        _db.Applications.Remove(application);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Deleted application {Id}", id);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
        => await _db.Applications.AnyAsync(a => a.Id == id);

    public async Task<Dictionary<ApplicationStatus, int>> GetStatisticsAsync(Guid? candidateId)
    {
        var query = _db.Applications.AsQueryable();
        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);

        return await query
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }

    public async Task<List<MonthlyTrendDto>> GetMonthlyTrendsAsync(Guid? candidateId, int months = 12)
    {
        var query = _db.Applications.AsQueryable();
        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);

        var cutoff = DateTime.UtcNow.AddMonths(-months);
        query = query.Where(a => a.AppliedAt >= cutoff);

        var raw = await query
            .GroupBy(a => new { a.AppliedAt.Year, a.AppliedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Status = g.GroupBy(a => a.Status)
                    .Select(sg => new { Status = sg.Key, Count = sg.Count() })
                    .ToList()
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        return raw.Select(r =>
        {
            var s = r.Status.ToDictionary(x => x.Status, x => x.Count);
            return new MonthlyTrendDto(
                r.Year, r.Month,
                s.GetValueOrDefault(ApplicationStatus.PENDING, 0),
                s.GetValueOrDefault(ApplicationStatus.REVIEWED, 0),
                s.GetValueOrDefault(ApplicationStatus.INTERVIEW, 0),
                s.GetValueOrDefault(ApplicationStatus.ACCEPTED, 0),
                s.GetValueOrDefault(ApplicationStatus.REJECTED, 0),
                s.GetValueOrDefault(ApplicationStatus.CANCELLED, 0)
            );
        }).ToList();
    }

    public async Task<double?> GetAverageResponseTimeAsync(Guid? candidateId)
    {
        var query = _db.Applications
            .Include(a => a.StatusHistory)
            .AsQueryable();

        if (candidateId.HasValue)
            query = query.Where(a => a.CandidateId == candidateId.Value);

        var appsWithResponse = await query
            .Where(a => a.StatusHistory.Any(h => h.NewStatus != ApplicationStatus.PENDING))
            .Select(a => new
            {
                a.AppliedAt,
                FirstResponse = a.StatusHistory
                    .Where(h => h.NewStatus != ApplicationStatus.PENDING)
                    .Min(h => h.ChangedAt)
            })
            .ToListAsync();

        if (appsWithResponse.Count == 0) return null;

        return appsWithResponse
            .Select(x => (x.FirstResponse - x.AppliedAt).TotalDays)
            .Average();
    }

    public async Task<bool> ToggleSaveAsync(Guid id)
    {
        var app = await _db.Applications.FindAsync(id);
        if (app == null) return false;

        app.IsSaved = !app.IsSaved;
        app.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Toggled save for application {Id} → {IsSaved}", id, app.IsSaved);
        return app.IsSaved;
    }

    public async Task<List<CalendarEventDto>> GetCalendarEventsAsync(Guid candidateId, DateTime from, DateTime to, string[]? statuses)
    {
        var events = new List<CalendarEventDto>();

        var parsedStatuses = statuses?.Select(s => Enum.TryParse<ApplicationStatus>(s, true, out var st) ? st : (ApplicationStatus?)null)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList() ?? [];

        if (parsedStatuses.Contains(ApplicationStatus.PENDING))
        {
            var appliedEvents = await _db.Applications
                .Where(a => a.CandidateId == candidateId
                    && a.AppliedAt >= from
                    && a.AppliedAt <= to)
                .Select(a => new CalendarEventDto(
                    a.AppliedAt.ToString("yyyy-MM-dd"),
                    "applied",
                    "Applied at " + a.CompanyName,
                    a.Id,
                    a.CompanyName,
                    a.PositionTitle
                ))
                .ToListAsync();

            events.AddRange(appliedEvents);
        }

        var statusEvents = await _db.ApplicationStatusHistory
            .Include(h => h.Application)
            .Where(h => h.Application != null
                && h.Application.CandidateId == candidateId
                && h.ChangedAt >= from
                && h.ChangedAt <= to
                && parsedStatuses.Contains(h.NewStatus)
                && h.NewStatus != ApplicationStatus.PENDING)
            .Select(h => new CalendarEventDto(
                h.ChangedAt.ToString("yyyy-MM-dd"),
                h.NewStatus.ToString().ToLower(),
                h.Application!.CompanyName + " - " + h.NewStatus.ToString(),
                h.ApplicationId,
                h.Application!.CompanyName,
                h.Application.PositionTitle
            ))
            .ToListAsync();

        events.AddRange(statusEvents);

        return events.OrderBy(e => e.Date).ToList();
    }

    public async Task<List<ActivityItemDto>> GetActivityFeedAsync(Guid? candidateId, int limit = 50)
    {
        var query = _db.ApplicationStatusHistory
            .Include(h => h.Application)
            .AsQueryable();

        if (candidateId.HasValue)
            query = query.Where(h => h.Application != null && h.Application.CandidateId == candidateId.Value);

        var items = await query
            .Where(h => h.Application != null)
            .OrderByDescending(h => h.ChangedAt)
            .Take(limit)
            .Select(h => new ActivityItemDto(
                h.ApplicationId,
                h.Application!.CompanyName,
                h.Application.PositionTitle,
                h.OldStatus != null ? h.OldStatus.ToString() : null,
                h.NewStatus.ToString(),
                h.ChangedAt,
                h.Comment
            ))
            .ToListAsync();

        return items;
    }

    public async Task<int> GetActivityFeedCountAsync(Guid? candidateId)
    {
        var query = _db.ApplicationStatusHistory
            .Include(h => h.Application)
            .AsQueryable();

        if (candidateId.HasValue)
            query = query.Where(h => h.Application != null && h.Application.CandidateId == candidateId.Value);

        return await query.CountAsync();
    }
}
