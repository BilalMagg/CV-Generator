using Microsoft.EntityFrameworkCore;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Application.Services;

public class EmailScheduleService
{
    private readonly NotificationDbContext _db;
    private readonly ILogger<EmailScheduleService> _logger;

    public EmailScheduleService(NotificationDbContext db, ILogger<EmailScheduleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<EmailScheduleDto>> GetSchedulesAsync(Guid userId)
    {
        return await _db.Set<EmailSchedule>()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new EmailScheduleDto
            {
                Id = s.Id, Name = s.Name, Subject = s.Subject, Body = s.Body,
                Cron = s.Cron, RecipientType = s.RecipientType,
                RecipientValue = s.RecipientValue, IsActive = s.IsActive,
                LastRunAt = s.LastRunAt, NextRunAt = s.NextRunAt, CreatedAt = s.CreatedAt
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
        var nextRun = ComputeNextRun(dto.Cron);
        var schedule = new EmailSchedule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Subject = dto.Subject,
            Body = dto.Body,
            Cron = dto.Cron,
            RecipientType = dto.RecipientType,
            RecipientValue = dto.RecipientValue,
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
        if (dto.Cron is not null) { s.Cron = dto.Cron; s.NextRunAt = ComputeNextRun(dto.Cron); }
        if (dto.RecipientType is not null) s.RecipientType = dto.RecipientType;
        if (dto.RecipientValue is not null) s.RecipientValue = dto.RecipientValue;
        if (dto.IsActive.HasValue) s.IsActive = dto.IsActive.Value;

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
        if (s.IsActive) s.NextRunAt = ComputeNextRun(s.Cron);
        else s.NextRunAt = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public List<EmailSchedule> GetDueSchedules()
    {
        var now = DateTime.UtcNow;
        return _db.Set<EmailSchedule>()
            .Where(s => s.IsActive && s.NextRunAt <= now)
            .ToList();
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

    private static EmailScheduleDto Map(EmailSchedule s) => new()
    {
        Id = s.Id, Name = s.Name, Subject = s.Subject, Body = s.Body,
        Cron = s.Cron, RecipientType = s.RecipientType,
        RecipientValue = s.RecipientValue, IsActive = s.IsActive,
        LastRunAt = s.LastRunAt, NextRunAt = s.NextRunAt, CreatedAt = s.CreatedAt
    };
}
