using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using CVGenerator.Shared;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.API.Controllers;

[Route("api/mailbox")]
public class MailboxController : BaseApiController
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
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var query = _db.Set<EmailMessage>()
            .Include(m => m.Contact)
            .Where(m => m.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(m => m.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(m =>
                m.Subject.ToLower().Contains(term) ||
                m.ToEmail.ToLower().Contains(term) ||
                (m.ToName != null && m.ToName.ToLower().Contains(term)));
        }

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
                UserId = m.UserId,
                ContactId = m.ContactId,
                RecipientEmail = m.ToEmail,
                RecipientName = m.ToName,
                Subject = m.Subject,
                Body = m.Body,
                Status = m.Status,
                ErrorMessage = m.Error,
                Provider = m.Provider,
                SentAt = m.SentAt,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<EmailHistoryResponse>.Ok(new EmailHistoryResponse
        {
            Items = items,
            Total = total,
            SentCount = sentCount,
            FailedCount = failedCount,
            DraftCount = draftCount
        }));
    }

    [HttpGet("history/{id}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var userId = GetUserId();
        var msg = await _db.Set<EmailMessage>()
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
        if (msg is null) return NotFound(ApiResponse<EmailMessageDto>.Error("Email not found"));

        return Ok(ApiResponse<EmailMessageDto>.Ok(new EmailMessageDto
        {
            Id = msg.Id,
            UserId = msg.UserId,
            ContactId = msg.ContactId,
            RecipientEmail = msg.ToEmail,
            RecipientName = msg.ToName,
            Subject = msg.Subject,
            Body = msg.Body,
            Status = msg.Status,
            ErrorMessage = msg.Error,
            Provider = msg.Provider,
            SentAt = msg.SentAt,
            CreatedAt = msg.CreatedAt
        }));
    }

    [HttpGet("contact-history/{contactId}")]
    public async Task<IActionResult> GetContactHistory(Guid contactId)
    {
        var userId = GetUserId();
        var contact = await _db.Set<Contact>()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.UserId == userId);
        if (contact is null)
            return NotFound(ApiResponse<object>.Error("Contact not found"));

        var emails = await _db.Set<EmailMessage>()
            .Where(m => m.ContactId == contactId && m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new EmailMessageDto
            {
                Id = m.Id,
                UserId = m.UserId,
                ContactId = m.ContactId,
                RecipientEmail = m.ToEmail,
                RecipientName = m.ToName,
                Subject = m.Subject,
                Body = m.Body,
                Status = m.Status,
                ErrorMessage = m.Error,
                Provider = m.Provider,
                SentAt = m.SentAt,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<ContactHistoryResponse>.Ok(new ContactHistoryResponse
        {
            Contact = new ContactDto
            {
                Id = contact.Id,
                UserId = contact.UserId,
                Name = contact.Name,
                Email = contact.Email,
                Phone = contact.Phone,
                Company = contact.Company,
                Position = contact.Position,
                Notes = contact.Notes,
                Source = contact.Source,
                IsFavorite = contact.IsFavorite,
                AvatarBase64 = contact.AvatarBase64,
                CreatedAt = contact.CreatedAt,
                UpdatedAt = contact.UpdatedAt
            },
            Emails = emails,
            TotalEmails = emails.Count
        }));
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendEmailDto dto)
    {
        var userId = GetUserId();
        if (dto.RecipientIds.Count == 0)
            return BadRequest(ApiResponse<object>.Error("No recipients specified"));

        var contacts = await _db.Set<Contact>()
            .Where(c => dto.RecipientIds.Contains(c.Id) && c.UserId == userId)
            .ToListAsync();

        if (contacts.Count == 0)
            return BadRequest(ApiResponse<object>.Error("No valid contacts found"));

        var connection = await _db.Set<GmailConnection>()
            .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsRevoked);

        var fromEmail = connection?.GmailAddress ?? "noreply@propel.com";
        var provider = connection is not null ? "gmail" : "smtp";

        var sent = 0;
        var failed = 0;

        foreach (var contact in contacts)
        {
            try
            {
                await _gmailSendSvc.SendWithAttachmentAsync(userId, contact.Email, dto.Subject, dto.Body, null);

                _db.Set<EmailMessage>().Add(new EmailMessage
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ContactId = contact.Id,
                    FromEmail = fromEmail,
                    ToEmail = contact.Email,
                    ToName = contact.Name,
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
                _logger.LogError(ex, "Failed to send email to {Email} for user {UserId}", contact.Email, userId);

                _db.Set<EmailMessage>().Add(new EmailMessage
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ContactId = contact.Id,
                    FromEmail = fromEmail,
                    ToEmail = contact.Email,
                    ToName = contact.Name,
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

        return Ok(ApiResponse<object>.Ok(new { sent, failed, total = contacts.Count }));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var userId = GetUserId();
        var totalSent = await _db.Set<EmailMessage>().CountAsync(m => m.UserId == userId && m.Status == "sent");
        var totalFailed = await _db.Set<EmailMessage>().CountAsync(m => m.UserId == userId && m.Status == "failed");
        var totalSchedules = await _db.Set<EmailSchedule>().CountAsync(s => s.UserId == userId && s.IsActive);
        var totalContacts = await _contactSvc.GetContactCountAsync(userId);
        var successRate = (totalSent + totalFailed) > 0
            ? Math.Round((double)totalSent / (totalSent + totalFailed) * 100, 1)
            : 100;

        return Ok(ApiResponse<MailboxStatsDto>.Ok(new MailboxStatsDto
        {
            EmailsSent = totalSent,
            ScheduledEmails = totalSchedules,
            Contacts = (int)totalContacts,
            SuccessRate = successRate
        }));
    }
}
