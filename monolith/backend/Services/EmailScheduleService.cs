using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

public class EmailScheduleService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EmailScheduleService> _logger;
    private readonly IGmailSendService _gmail;

    public EmailScheduleService(AppDbContext db, ILogger<EmailScheduleService> logger, IGmailSendService gmail)
    {
        _db = db;
        _logger = logger;
        _gmail = gmail;
    }

    public async Task<List<EmailScheduleDto>> GetSchedulesAsync(Guid userId)
    {
        return await _db.Set<EmailSchedule>()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new EmailScheduleDto
            {
                Id = s.Id, UserId = s.UserId, Name = s.Name, Subject = s.Subject, Body = s.Body,
                CronExpression = s.CronExpression,
                RecipientIds = s.RecipientIds, IsActive = s.IsActive,
                LastRunAt = s.LastRunAt, NextRunAt = s.NextRunAt,
                CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<EmailScheduleDto?> GetScheduleAsync(Guid id, Guid userId)
    {
        var s = await _db.Set<EmailSchedule>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s is null) return null;
        return Map(s);
    }

    public async Task<EmailScheduleDto> CreateScheduleAsync(Guid userId, CreateScheduleDto dto)
    {
        var nextRun = ComputeNextRun(dto.CronExpression);
        var schedule = new EmailSchedule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Subject = dto.Subject,
            Body = dto.Body,
            CronExpression = dto.CronExpression,
            RecipientIds = dto.RecipientIds,
            NextRunAt = nextRun
        };
        _db.Set<EmailSchedule>().Add(schedule);
        await _db.SaveChangesAsync();
        return Map(schedule);
    }

    public async Task<EmailScheduleDto?> UpdateScheduleAsync(Guid id, Guid userId, UpdateScheduleDto dto)
    {
        var s = await _db.Set<EmailSchedule>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s is null) return null;

        if (dto.Name is not null) s.Name = dto.Name;
        if (dto.Subject is not null) s.Subject = dto.Subject;
        if (dto.Body is not null) s.Body = dto.Body;
        if (dto.CronExpression is not null) { s.CronExpression = dto.CronExpression; s.NextRunAt = ComputeNextRun(dto.CronExpression); }
        if (dto.RecipientIds is not null) s.RecipientIds = dto.RecipientIds;
        s.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Map(s);
    }

    public async Task<bool> DeleteScheduleAsync(Guid id, Guid userId)
    {
        var s = await _db.Set<EmailSchedule>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s is null) return false;
        _db.Set<EmailSchedule>().Remove(s);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleScheduleAsync(Guid id, Guid userId)
    {
        var s = await _db.Set<EmailSchedule>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s is null) return false;
        s.IsActive = !s.IsActive;
        if (s.IsActive) s.NextRunAt = ComputeNextRun(s.CronExpression);
        else s.NextRunAt = null;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public List<EmailSchedule> GetDueSchedules()
    {
        var now = DateTime.UtcNow;
        return _db.Set<EmailSchedule>()
            .Where(s => s.IsActive && s.NextRunAt != null && s.NextRunAt <= now)
            .ToList();
    }

    /// <summary>
    /// Fires one schedule immediately (ownership-checked): sends to all recipients,
    /// records EmailMessage rows stamped with the schedule id, advances the cron.
    /// Returns null when the schedule doesn't exist; a parked result (sent=-1) means
    /// Gmail isn't connected — NextRunAt is left untouched so it fires on reconnect.
    /// </summary>
    public async Task<(int Sent, int Failed)?> RunNowAsync(Guid id, Guid userId)
    {
        var schedule = await _db.Set<EmailSchedule>()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (schedule is null) return null;

        return await FireScheduleAsync(schedule);
    }

    /// <summary>Shared fire pipeline used by both the background worker and Run-now.</summary>
    public async Task<(int Sent, int Failed)> FireScheduleAsync(EmailSchedule schedule)
    {
        var userId = schedule.UserId;
        var now = DateTime.UtcNow;

        var contacts = await _db.Set<Contact>()
            .Where(c => schedule.RecipientIds.Contains(c.Id) && c.UserId == userId)
            .ToListAsync();

        var connection = await _db.Set<GmailConnection>()
            .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsRevoked);

        if (connection is null)
        {
            _logger.LogWarning(
                "Schedule {Name} ({Id}) for user {UserId} is due but Gmail is not connected — leaving for next tick",
                schedule.Name, schedule.Id, userId);
            // Don't advance NextRunAt: the moment the user connects Gmail it fires.
            return (-1, 0);
        }

        if (contacts.Count == 0)
        {
            _logger.LogWarning("Schedule {Name} ({Id}) has no valid recipients — parking it", schedule.Name, schedule.Id);
            schedule.LastRunAt = now;
            schedule.NextRunAt = null; // invalid state; user must edit/re-enable
            schedule.UpdatedAt = now;
            await _db.SaveChangesAsync();
            return (0, 0);
        }

        int sent = 0, failed = 0;

        foreach (var contact in contacts)
        {
            try
            {
                await _gmail.SendWithAttachmentAsync(userId, contact.Email, schedule.Subject, schedule.Body);

                _db.Set<EmailMessage>().Add(new EmailMessage
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ScheduleId = schedule.Id,
                    ContactId = contact.Id,
                    FromEmail = connection.GmailAddress,
                    ToEmail = contact.Email,
                    ToName = contact.Name,
                    Subject = schedule.Subject,
                    Body = schedule.Body,
                    Status = "sent",
                    Provider = "gmail",
                    SentAt = now
                });
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled email to {Email} (schedule {Id}) failed", contact.Email, schedule.Id);

                _db.Set<EmailMessage>().Add(new EmailMessage
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ScheduleId = schedule.Id,
                    ContactId = contact.Id,
                    FromEmail = connection.GmailAddress,
                    ToEmail = contact.Email,
                    ToName = contact.Name,
                    Subject = schedule.Subject,
                    Body = schedule.Body,
                    Status = "failed",
                    Error = ex.Message,
                    Provider = "gmail"
                });
                failed++;
            }
        }

        schedule.LastRunAt = now;
        schedule.NextRunAt = ComputeNextRun(schedule.CronExpression);
        schedule.UpdatedAt = now;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Schedule {Name} ({Id}) fired: {Sent} sent / {Failed} failed; next run {NextRun:O}",
            schedule.Name, schedule.Id, sent, failed, schedule.NextRunAt);

        return (sent, failed);
    }

    /// <summary>Sent/failed email rows recorded for this schedule.</summary>
    public async Task<(List<EmailMessage> Items, int Total)> GetHistoryAsync(Guid id, Guid userId, int page, int pageSize)
    {
        var query = _db.Set<EmailMessage>().AsNoTracking()
            .Include(m => m.Contact)
            .Where(m => m.ScheduleId == id && m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    private static DateTime? ComputeNextRun(string cron)
    {
        try
        {
            var expression = Cronos.CronExpression.Parse(cron);
            return expression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
        }
        catch
        {
            return null;
        }
    }

    private static List<DateTime>? ComputeUpcomingRuns(string cron)
    {
        try
        {
            var expression = Cronos.CronExpression.Parse(cron);
            var runs = new List<DateTime>();
            var next = expression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
            for (var i = 0; i < 3 && next.HasValue; i++)
            {
                runs.Add(next.Value);
                next = expression.GetNextOccurrence(next.Value.AddMinutes(1), TimeZoneInfo.Utc);
            }
            return runs;
        }
        catch
        {
            return null;
        }
    }

    private static EmailScheduleDto Map(EmailSchedule s) => new()
    {
        Id = s.Id, UserId = s.UserId, Name = s.Name, Subject = s.Subject, Body = s.Body,
        CronExpression = s.CronExpression,
        RecipientIds = s.RecipientIds, IsActive = s.IsActive,
        LastRunAt = s.LastRunAt, NextRunAt = s.NextRunAt,
        UpcomingRuns = ComputeUpcomingRuns(s.CronExpression),
        CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt
    };
}
