using Microsoft.EntityFrameworkCore;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Application.Services;

public class ReminderService : IReminderService
{
    private readonly NotificationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ITemplateRenderer _renderer;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(
        NotificationDbContext db,
        IEmailService emailService,
        ITemplateRenderer renderer,
        ILogger<ReminderService> logger)
    {
        _db = db;
        _emailService = emailService;
        _renderer = renderer;
        _logger = logger;
    }

    public async Task<Guid> CreateReminderAsync(CreateReminderDto dto)
    {
        var offset = Enum.TryParse<ReminderOffset>(dto.ReminderOffset, ignoreCase: true, out var parsed)
            ? parsed
            : ReminderOffset.OneDay;

        var reminderAt = ComputeReminderAt(dto.EventDate, offset);

        var reminder = new Reminder
        {
            UserId = dto.UserId,
            UserEmail = dto.UserEmail,
            UserFirstName = dto.UserFirstName,
            Title = dto.Title,
            Message = dto.Message,
            EventDate = dto.EventDate,
            ReminderOffset = offset,
            ReminderAt = reminderAt,
            Status = ReminderStatus.Pending,
        };

        _db.Reminders.Add(reminder);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Reminder created: {ReminderId} for user {UserId}, fires at {ReminderAt}",
            reminder.Id, reminder.UserId, reminder.ReminderAt);

        return reminder.Id;
    }

    public async Task<List<ReminderResultDto>> GetUserRemindersAsync(Guid userId)
    {
        return await _db.Reminders
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.EventDate)
            .Select(r => new ReminderResultDto
            {
                Id = r.Id,
                Title = r.Title,
                Message = r.Message,
                EventDate = r.EventDate,
                ReminderOffset = r.ReminderOffset.ToString(),
                ReminderAt = r.ReminderAt,
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt,
                SentAt = r.SentAt,
            })
            .ToListAsync();
    }

    public async Task CancelReminderAsync(Guid reminderId)
    {
        var reminder = await _db.Reminders.FindAsync(reminderId);
        if (reminder is null)
        {
            _logger.LogWarning("Attempted to cancel non-existent reminder: {ReminderId}", reminderId);
            return;
        }

        if (reminder.Status != ReminderStatus.Pending)
        {
            _logger.LogWarning("Cannot cancel reminder {ReminderId} — status is {Status}", reminderId, reminder.Status);
            return;
        }

        reminder.Status = ReminderStatus.Cancelled;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Reminder {ReminderId} cancelled", reminderId);
    }

    public async Task ProcessDueRemindersAsync()
    {
        var now = DateTime.UtcNow;

        var dueReminders = await _db.Reminders
            .Where(r => r.ReminderAt <= now && r.Status == ReminderStatus.Pending)
            .ToListAsync();

        if (dueReminders.Count == 0)
            return;

        _logger.LogInformation("Processing {Count} due reminders", dueReminders.Count);

        foreach (var reminder in dueReminders)
        {
            try
            {
                var htmlBody = await _renderer.RenderAsync("user-reminder", new
                {
                    first_name = reminder.UserFirstName,
                    title = reminder.Title,
                    message = reminder.Message ?? "",
                    event_date = reminder.EventDate.ToString("dddd, MMMM d, yyyy"),
                });

                await _emailService.SendAsync(
                    reminder.UserEmail,
                    reminder.UserFirstName,
                    $"⏰ Reminder: {reminder.Title}",
                    htmlBody);

                reminder.Status = ReminderStatus.Sent;
                reminder.SentAt = DateTime.UtcNow;

                _logger.LogInformation("Reminder {ReminderId} sent to {Email}", reminder.Id, reminder.UserEmail);
            }
            catch (Exception ex)
            {
                reminder.Status = ReminderStatus.Failed;
                _logger.LogError(ex, "Failed to send reminder {ReminderId} to {Email}", reminder.Id, reminder.UserEmail);
            }
        }

        await _db.SaveChangesAsync();
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static DateTime ComputeReminderAt(DateTime eventDate, ReminderOffset offset)
    {
        return offset switch
        {
            ReminderOffset.None => eventDate,
            ReminderOffset.OneDay => eventDate.AddDays(-1),
            ReminderOffset.TwoDays => eventDate.AddDays(-2),
            ReminderOffset.ThreeDays => eventDate.AddDays(-3),
            ReminderOffset.OneWeek => eventDate.AddDays(-7),
            _ => eventDate.AddDays(-1),
        };
    }
}
