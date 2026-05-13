using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IEmailService _emailService;
    private readonly ITemplateRenderer _renderer;
    private readonly NotificationDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IEmailService emailService,
        ITemplateRenderer renderer,
        NotificationDbContext db,
        ILogger<NotificationService> logger)
    {
        _emailService = emailService;
        _renderer = renderer;
        _db = db;
        _logger = logger;
    }

    public async Task SendWelcomeAsync(Guid userId, string email, string firstName)
    {
        var body = await _renderer.RenderAsync("welcome", new { first_name = firstName });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.Welcome,
            "Welcome to CV Generator 🎉", body);
    }

    public async Task SendCvGeneratedAsync(Guid userId, string email, string firstName, string cvDownloadUrl)
    {
        var body = await _renderer.RenderAsync("cv-ready", new
        {
            first_name = firstName,
            download_url = cvDownloadUrl
        });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.CvGenerated,
            "Your CV is ready to download!", body);
    }

    public async Task SendApplicationCreatedAsync(Guid userId, string email, string firstName, string company, string position)
    {
        var body = await _renderer.RenderAsync("application-created", new
        {
            first_name = firstName,
            company,
            position
        });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.ApplicationCreated,
            $"Application sent to {company} ✅", body);
    }

    public async Task SendApplicationStatusChangedAsync(Guid userId, string email, string firstName, string company, string position, string newStatus)
    {
        var body = await _renderer.RenderAsync("status-changed", new
        {
            first_name = firstName,
            company,
            position,
            status = newStatus
        });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.ApplicationStatusChanged,
            $"Your application at {company} has been updated", body);
    }

    public async Task SendNoResponseReminderAsync(Guid userId, string email, string firstName, string company, string position, int daysSinceApplied)
    {
        var body = await _renderer.RenderAsync("application-reminder", new
        {
            first_name = firstName,
            company,
            position,
            days = daysSinceApplied
        });
        var type = daysSinceApplied >= 14
            ? NotificationType.ApplicationNoResponseTwoWeeks
            : NotificationType.ApplicationNoResponseOneWeek;

        await SaveAndSendAsync(userId, email, firstName, type,
            $"No reply from {company} after {daysSinceApplied} days — time to follow up?", body);
    }

    public async Task SendWeeklyDigestAsync(Guid userId, string email, string firstName,
        int totalApplications, int responses, int cvs)
    {
        var body = await _renderer.RenderAsync("weekly-digest", new
        {
            first_name = firstName,
            total_applications = totalApplications,
            responses,
            cvs_generated = cvs
        });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.WeeklyDigest,
            "Your weekly CV Generator summary 📊", body);
    }

    public async Task<List<NotificationResultDto>> GetUserNotificationsAsync(Guid userId)
    {
        return _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationResultDto
            {
                Id = n.Id,
                Type = n.Type,
                Subject = n.Subject,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToList();
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await _db.Notifications.FindAsync(notificationId);
        if (notification is null) return;
        notification.IsRead = true;
        await _db.SaveChangesAsync();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task SaveAndSendAsync(Guid userId, string email, string name,
        NotificationType type, string subject, string htmlBody)
    {
        var entity = new Notification
        {
            UserId = userId,
            UserEmail = email,
            Type = type,
            Channel = NotificationChannel.Email,
            Subject = subject,
            Body = htmlBody
        };

        _db.Notifications.Add(entity);

        try
        {
            await _emailService.SendAsync(email, name, subject, htmlBody);
            entity.IsSent = true;
            entity.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            entity.IsSent = false;
            entity.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Notification of type {Type} failed for user {UserId}", type, userId);
        }

        await _db.SaveChangesAsync();
    }
}