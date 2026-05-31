using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/mailbox")]
public class MailboxController : ControllerBase
{
    private readonly NotificationDbContext _db;
    private readonly IGmailSendService _gmailSendSvc;
    private readonly IContactService _contactSvc;
    private readonly ILogger<MailboxController> _logger;

    public MailboxController(
        NotificationDbContext db,
        IGmailSendService gmailSendSvc,
        IContactService contactSvc,
        ILogger<MailboxController> logger)
    {
        _db = db;
        _gmailSendSvc = gmailSendSvc;
        _contactSvc = contactSvc;
        _logger = logger;
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid userId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Set<EmailMessage>().Where(m => m.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(m => m.Status == status);

        var total = await query.CountAsync();
        var sentCount = await query.CountAsync(m => m.Status == "sent");
        var failedCount = await query.CountAsync(m => m.Status == "failed");
        var draftCount = await query.CountAsync(m => m.Status == "draft");

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new EmailMessageDto
            {
                Id = m.Id,
                ToEmail = m.ToEmail,
                ToName = m.ToName,
                Subject = m.Subject,
                Body = m.Body,
                Status = m.Status,
                Error = m.Error,
                Provider = m.Provider,
                SentAt = m.SentAt,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(new EmailHistoryResponse
        {
            Items = items,
            Total = total,
            SentCount = sentCount,
            FailedCount = failedCount,
            DraftCount = draftCount
        });
    }

    [HttpGet("history/{id}")]
    public async Task<IActionResult> GetDetail(Guid id, [FromQuery] Guid userId)
    {
        var msg = await _db.Set<EmailMessage>()
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
        if (msg is null) return NotFound();

        return Ok(new EmailMessageDto
        {
            Id = msg.Id,
            ToEmail = msg.ToEmail,
            ToName = msg.ToName,
            Subject = msg.Subject,
            Body = msg.Body,
            Status = msg.Status,
            Error = msg.Error,
            Provider = msg.Provider,
            SentAt = msg.SentAt,
            CreatedAt = msg.CreatedAt
        });
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendEmailDto dto)
    {
        if (dto.ToEmails.Count == 0)
            return BadRequest(new { error = "No recipients specified" });

        var sent = 0;
        var failed = 0;

        var connection = await _db.Set<GmailConnection>()
            .FirstOrDefaultAsync(c => c.UserId == dto.UserId && !c.IsRevoked);

        var fromEmail = connection?.GmailAddress ?? "noreply@propel.com";
        var provider = connection is not null ? "gmail" : "smtp";

        foreach (var to in dto.ToEmails)
        {
            try
            {
                await _gmailSendSvc.SendWithAttachmentAsync(dto.UserId, to, dto.Subject, dto.Body, dto.CvPdfUrl);

                _db.Set<EmailMessage>().Add(new EmailMessage
                {
                    Id = Guid.NewGuid(),
                    UserId = dto.UserId,
                    FromEmail = fromEmail,
                    ToEmail = to,
                    Subject = dto.Subject,
                    Body = dto.Body,
                    Status = "sent",
                    Provider = provider,
                    SentAt = DateTime.UtcNow
                });
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To} for user {UserId}", to, dto.UserId);

                _db.Set<EmailMessage>().Add(new EmailMessage
                {
                    Id = Guid.NewGuid(),
                    UserId = dto.UserId,
                    FromEmail = fromEmail,
                    ToEmail = to,
                    Subject = dto.Subject,
                    Body = dto.Body,
                    Status = "failed",
                    Error = ex.Message,
                    Provider = provider
                });
                failed++;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new { sent, failed, total = dto.ToEmails.Count });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] Guid userId)
    {
        var totalSent = await _db.Set<EmailMessage>().CountAsync(m => m.UserId == userId && m.Status == "sent");
        var totalFailed = await _db.Set<EmailMessage>().CountAsync(m => m.UserId == userId && m.Status == "failed");
        var totalSchedules = await _db.Set<EmailSchedule>().CountAsync(s => s.UserId == userId && s.IsActive);
        var totalContacts = await _contactSvc.GetContactCountAsync(userId);
        var successRate = (totalSent + totalFailed) > 0
            ? Math.Round((double)totalSent / (totalSent + totalFailed) * 100, 1)
            : 100;

        return Ok(new MailboxStatsDto
        {
            TotalSent = totalSent,
            TotalFailed = totalFailed,
            TotalContacts = (int)totalContacts,
            TotalSchedules = totalSchedules,
            SuccessRate = successRate
        });
    }
}
