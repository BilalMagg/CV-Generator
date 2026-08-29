using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

/// <summary>
/// One-stop paste-and-apply pipeline used by the Apply wizard:
/// upserts the company + contact, creates a tracked Application (SAVED), then either
/// sends the email immediately (logs a USER/SENT attempt) or schedules it
/// (creates an EmailSchedule + SCHEDULED attempt). The SAVED → APPLIED transition happens
/// only on a successful actual send.
/// </summary>
public class ApplyService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ApplyService> _logger;
    private readonly IGmailSendService _gmail;
    private readonly IMinioStorageService _minio;
    private readonly IApplicationService _applications;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string AttachmentBucket = "mail-attachments";

    public ApplyService(
        AppDbContext db,
        ILogger<ApplyService> logger,
        IGmailSendService gmail,
        IMinioStorageService minio,
        IApplicationService applications,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _logger = logger;
        _gmail = gmail;
        _minio = minio;
        _applications = applications;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ApplyEmailResult> ApplyAsync(Guid userId, ApplyEmailRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CompanyName)) throw new ArgumentException("CompanyName is required");
        if (string.IsNullOrWhiteSpace(dto.RecipientEmail)) throw new ArgumentException("RecipientEmail is required");
        if (string.IsNullOrWhiteSpace(dto.Subject)) throw new ArgumentException("Subject is required");
        if (string.IsNullOrWhiteSpace(dto.Body)) throw new ArgumentException("Body is required");

        var company = await UpsertCompanyAsync(userId, dto.CompanyName.Trim(), dto.CompanyDescription);
        var contact = await UpsertContactAsync(userId, company, dto.RecipientEmail.Trim(), dto.RecipientName, dto.ContactNotes);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found");

        // Resolve {{token}} placeholders (company_name, my_name, ...) before sending/scheduling.
        var subject = TemplateVariableResolver.Render(dto.Subject, null, company, user);
        var body = TemplateVariableResolver.Render(dto.Body, null, company, user);

        var app = await _applications.CreateAsync(new CreateApplicationDto(
            CandidateId: userId,
            CvVersionId: dto.CvVersionId,
            JobOfferId: null,
            CompanyName: company.Name,
            PositionTitle: dto.PositionTitle.Trim(),
            OfferSource: "job_post_apply",
            Notes: null,
            Origin: "MANUAL",
            Status: "SAVED",
            AllowDuplicate: dto.AllowDuplicate
        ), userId);

        if (!string.IsNullOrWhiteSpace(dto.ScheduleCron))
        {
            var cronError = EmailScheduleService.ValidateCron(dto.ScheduleCron);
            if (cronError != null) throw new ArgumentException(cronError);

            var refs = await UploadAttachmentsAsync(userId, dto.Attachments);

            var schedule = new EmailSchedule
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = dto.ScheduleName ?? $"Apply → {company.Name}",
                Subject = subject,
                Body = body,
                CronExpression = dto.ScheduleCron,
                RecipientIds = [contact.Id],
                ApplicationId = app.Id,
                CvVersionId = dto.CvVersionId,
                AttachmentRefsJson = refs.Count > 0 ? JsonSerializer.Serialize(refs) : null,
                NextRunAt = EmailScheduleService.ComputeNextRun(dto.ScheduleCron)
            };
            _db.Set<EmailSchedule>().Add(schedule);
            await _db.SaveChangesAsync();

            await _applications.CreateAttemptAsync(app.Id, new CreateAttemptDto(
                Channel: "EMAIL_GMAIL",
                InitiatedBy: "SCHEDULE",
                Status: "SCHEDULED",
                Subject: subject,
                Body: body,
                ContactId: contact.Id,
                CvVersionId: dto.CvVersionId,
                SentAt: null
            ), userId);

            _logger.LogInformation("Apply wizard scheduled email for application {App} (schedule {Sched})", app.Id, schedule.Id);

            return new ApplyEmailResult
            {
                ApplicationId = app.Id,
                ScheduleId = schedule.Id,
                ContactId = contact.Id,
                CompanyId = company.Id,
                SentNow = false
            };
        }

        // Send now
        var cvPdfUrl = await SkipDuplicateCvAsync(await ResolveCvPdfUrlAsync(dto.CvVersionId), dto.Attachments);
        try
        {
            var (messageId, threadId) = await _gmail.SendWithAttachmentAsync(
                userId, contact.Email, subject, body, cvPdfUrl, dto.Attachments);

            var connection = await _db.Set<GmailConnection>()
                .FirstOrDefaultAsync(c => c.UserId == userId && !c.IsRevoked);

            _db.Set<EmailMessage>().Add(new EmailMessage
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ContactId = contact.Id,
                FromEmail = connection?.GmailAddress ?? "noreply@propel.com",
                ToEmail = contact.Email,
                ToName = contact.Name,
                Subject = subject,
                Body = body,
                Status = "sent",
                Provider = connection is not null ? "gmail" : "smtp",
                SentAt = DateTime.UtcNow,
                AttachmentMetadataJson = dto.Attachments is { Count: > 0 }
                    ? JsonSerializer.Serialize(dto.Attachments.Select(a => new EmailAttachmentInfoDto
                    {
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        SizeBytes = (long)(a.ContentBase64.Length * 3L / 4)
                    }).ToList())
                    : null
            });
            await _db.SaveChangesAsync();

            var attempt = await _applications.CreateAttemptAsync(app.Id, new CreateAttemptDto(
                Channel: "EMAIL_GMAIL",
                InitiatedBy: "USER",
                Status: "SENT",
                Subject: subject,
                Body: body,
                ContactId: contact.Id,
                CvVersionId: dto.CvVersionId,
                ChannelMetadataJson: (messageId ?? threadId) is not null
                    ? JsonSerializer.Serialize(new { messageId, threadId })
                    : null,
                SentAt: DateTime.UtcNow
            ), userId);

            return new ApplyEmailResult
            {
                ApplicationId = app.Id,
                AttemptId = attempt.Id,
                ContactId = contact.Id,
                CompanyId = company.Id,
                SentNow = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply wizard send failed for application {App}", app.Id);
            throw;
        }
    }

    /// <summary>
    /// Creates the tracking shell (Company + optional Contact + SAVED Application) used by the
    /// "Apply Prep" flows (form answers / direct message) which do not send an email. Returns the
    /// new Application id. No attempt or email is recorded.
    /// </summary>
    public async Task<Guid> CreateTrackedApplicationAsync(Guid userId, ApplyPrepTrackedRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.CompanyName)) throw new ArgumentException("CompanyName is required");

        var company = await UpsertCompanyAsync(userId, r.CompanyName.Trim(), r.CompanyDescription);
        if (!string.IsNullOrWhiteSpace(r.RecipientEmail))
        {
            await UpsertContactAsync(userId, company, r.RecipientEmail.Trim(), r.RecipientName, r.ContactNotes);
        }

        var app = await _applications.CreateAsync(new CreateApplicationDto(
            CandidateId: userId,
            CvVersionId: r.CvVersionId,
            JobOfferId: null,
            CompanyName: company.Name,
            PositionTitle: r.PositionTitle.Trim(),
            OfferSource: "job_post_apply",
            Notes: null,
            Origin: "MANUAL",
            Status: "SAVED",
            AllowDuplicate: false
        ), userId);

        return app.Id;
    }

    private async Task<Company> UpsertCompanyAsync(Guid userId, string name, string? description)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c =>
            c.UserId == userId && c.Name.ToLower() == name.ToLower());
        if (company is null)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = name,
                Description = description,
                Country = "Morocco",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Companies.Add(company);
        }
        else if (string.IsNullOrWhiteSpace(company.Description) && !string.IsNullOrWhiteSpace(description))
        {
            company.Description = description;
        }
        await _db.SaveChangesAsync();
        return company;
    }

    private async Task<Contact> UpsertContactAsync(Guid userId, Company company, string email, string? name, string? notes)
    {
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.UserId == userId && c.Email == email);
        if (contact is null)
        {
            contact = new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = string.IsNullOrWhiteSpace(name) ? company.Name : name.Trim(),
                Email = email,
                Company = company.Name,
                Notes = notes,
                Source = "apply_wizard",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Contacts.Add(contact);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(contact.Name))
                contact.Name = name.Trim();
            if (string.IsNullOrWhiteSpace(contact.Company))
                contact.Company = company.Name;
            if (!string.IsNullOrWhiteSpace(notes) && string.IsNullOrWhiteSpace(contact.Notes))
                contact.Notes = notes;
            contact.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return contact;
    }

    private async Task<string?> ResolveCvPdfUrlAsync(Guid? cvVersionId)
    {
        if (cvVersionId is null) return null;
        var cv = await _db.CvVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == cvVersionId.Value);
        return cv?.PdfUrl;
    }

    /// <summary>
    /// If one of the explicit attachments is byte-identical to the auto-attached CV, drop the
    /// auto CV so the recipient doesn't receive the same file twice.
    /// </summary>
    private async Task<string?> SkipDuplicateCvAsync(string? cvPdfUrl, List<EmailAttachmentDto>? attachments)
    {
        if (cvPdfUrl is null || attachments is not { Count: > 0 }) return cvPdfUrl;
        try
        {
            using var http = _httpClientFactory.CreateClient();
            var cvBytes = await http.GetByteArrayAsync(cvPdfUrl);
            using var cvHash = SHA256.Create();
            var cvDigest = cvHash.ComputeHash(cvBytes);
            foreach (var att in attachments)
            {
                if (string.IsNullOrWhiteSpace(att.ContentBase64)) continue;
                try
                {
                    var attBytes = Convert.FromBase64String(att.ContentBase64);
                    using var h = SHA256.Create();
                    if (cvDigest.AsSpan().SequenceEqual(h.ComputeHash(attBytes)))
                    {
                        _logger.LogInformation("Skipping auto CV attach: identical file already present ({Name})", att.FileName);
                        return null;
                    }
                }
                catch (FormatException) { /* skip unreadable attachment */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CV duplicate check failed; attaching CV anyway");
        }
        return cvPdfUrl;
    }

    private async Task<List<ScheduleAttachmentRef>> UploadAttachmentsAsync(Guid userId, List<EmailAttachmentDto>? attachments)
    {
        if (attachments is not { Count: > 0 }) return [];
        var refs = new List<ScheduleAttachmentRef>();
        foreach (var att in attachments)
        {
            if (string.IsNullOrWhiteSpace(att.ContentBase64)) continue;
            try
            {
                var bytes = Convert.FromBase64String(att.ContentBase64);
                var objectKey = $"{userId}/{Guid.NewGuid()}_{att.FileName}";
                await _minio.UploadAsync(AttachmentBucket, objectKey, new MemoryStream(bytes), att.ContentType, bytes.Length);
                refs.Add(new ScheduleAttachmentRef
                {
                    FileName = att.FileName,
                    ContentType = att.ContentType,
                    ObjectKey = objectKey,
                    SizeBytes = bytes.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload attachment {File} for apply schedule", att.FileName);
            }
        }
        return refs;
    }
}