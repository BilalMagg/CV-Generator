using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Jobs;

public class EmailScheduleJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailScheduleJob> _logger;

    public EmailScheduleJob(IServiceScopeFactory scopeFactory, ILogger<EmailScheduleJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var scheduleSvc = scope.ServiceProvider.GetRequiredService<EmailScheduleService>();
        var gmailSendSvc = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IGmailSendService>();

        var due = scheduleSvc.GetDueSchedules();
        _logger.LogInformation("EmailScheduleJob: {Count} schedules due", due.Count);

        foreach (var schedule in due)
        {
            try
            {
                var recipients = schedule.RecipientType switch
                {
                    "contacts" => await db.Set<Domain.Entities.Contact>()
                        .Where(c => c.UserId == schedule.UserId)
                        .Select(c => c.Email)
                        .ToListAsync(),
                    _ => schedule.RecipientValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim()).ToList()
                };

                foreach (var to in recipients)
                {
                    try
                    {
                        await gmailSendSvc.SendWithAttachmentAsync(schedule.UserId, to, schedule.Subject, schedule.Body);

                        db.Set<Domain.Entities.EmailMessage>().Add(new Domain.Entities.EmailMessage
                        {
                            Id = Guid.NewGuid(),
                            UserId = schedule.UserId,
                            ToEmail = to,
                            Subject = schedule.Subject,
                            Body = schedule.Body,
                            Status = "sent",
                            Provider = "gmail",
                            SentAt = DateTime.UtcNow,
                            ScheduleId = schedule.Id
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Schedule email failed to {To}", to);
                        db.Set<Domain.Entities.EmailMessage>().Add(new Domain.Entities.EmailMessage
                        {
                            Id = Guid.NewGuid(),
                            UserId = schedule.UserId,
                            ToEmail = to,
                            Subject = schedule.Subject,
                            Body = schedule.Body,
                            Status = "failed",
                            Error = ex.Message,
                            Provider = "gmail",
                            ScheduleId = schedule.Id
                        });
                    }
                }

                schedule.LastRunAt = DateTime.UtcNow;
                schedule.NextRunAt = ComputeNextRun(schedule.Cron);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing schedule {ScheduleId}", schedule.Id);
            }
        }
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
}
