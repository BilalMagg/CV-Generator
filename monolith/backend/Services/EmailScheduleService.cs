using System.Text.Json;
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
    private readonly IMinioStorageService _minio;
    private readonly IApplicationService _applications;
    private const string AttachmentBucket = "mail-attachments";

    public EmailScheduleService(
        AppDbContext db,
        ILogger<EmailScheduleService> logger,
        IGmailSendService gmail,
        IMinioStorageService minio,
        IApplicationService applications)
    {
        _db = db;
        _logger = logger;
        _gmail = gmail;
        _minio = minio;
        _applications = applications;
    }

    public async Task<List<EmailScheduleDto>> GetSchedulesAsync(Guid userId)
    {
        return await _db.Set<EmailSchedule>()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => Map(s))
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
            ApplicationId = dto.ApplicationId,
            CvVersionId = dto.CvVersionId,
            TemplateSourceId = dto.TemplateSourceId,
            AttachmentRefsJson = SerializeRefs(dto.Attachments),
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
        if (dto.ApplicationId.HasValue) s.ApplicationId = dto.ApplicationId;
        if (dto.CvVersionId.HasValue) s.CvVersionId = dto.CvVersionId;
        if (dto.TemplateSourceId.HasValue) s.TemplateSourceId = dto.TemplateSourceId;
        if (dto.Attachments is not null) s.AttachmentRefsJson = SerializeRefs(dto.Attachments);
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

    public async Task<(int Sent, int Failed)?> RunNowAsync(Guid id, Guid userId)
    {
        var schedule = await _db.Set<EmailSchedule>()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (schedule is null) return null;

        return await FireScheduleAsync(schedule);
    }

    public async Task<ApplyTemplateResultDto> ApplyTemplateAsync(Guid userId, ApplyTemplateDto dto)
    {
        var template = await _db.ScheduleTemplates
            .FirstOrDefaultAsync(t => t.Id == dto.TemplateId && t.UserId == userId);
        if (template is null)
            throw new KeyNotFoundException("Template not found");

        if (string.IsNullOrWhiteSpace(dto.CronExpression))
            throw new ArgumentException("CronExpression is required");

        var cronError = ValidateCron(dto.CronExpression);
        if (cronError != null)
            throw new ArgumentException(cronError);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found");

        // Resolve company
        Company company;
        if (dto.CompanyId.HasValue)
        {
            company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == dto.CompanyId && c.UserId == userId)
                ?? throw new KeyNotFoundException("Company not found");
        }
        else if (!string.IsNullOrWhiteSpace(dto.CompanyName))
        {
            var name = dto.CompanyName.Trim();
            company = await _db.Companies.FirstOrDefaultAsync(c =>
                c.UserId == userId && c.Name.ToLower() == name.ToLower())
                ?? new Company
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = name,
                    Description = dto.CompanyDescription,
                    Country = "Morocco",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
            if (company.Id == Guid.Empty) _db.Companies.Add(company);
            else if (!string.IsNullOrWhiteSpace(dto.CompanyDescription) && string.IsNullOrWhiteSpace(company.Description))
                company.Description = dto.CompanyDescription;
        }
        else
        {
            throw new ArgumentException("CompanyId or CompanyName is required");
        }

        await _db.SaveChangesAsync();

        // Resolve recipient contact
        Contact contact;
        if (dto.RecipientContactId.HasValue)
        {
            contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == dto.RecipientContactId && c.UserId == userId)
                ?? throw new KeyNotFoundException("Recipient contact not found");
        }
        else
        {
            contact = await ResolveContactForCompanyAsync(userId, company, dto.RecipientEmail);
            if (contact is null)
                throw new ArgumentException($"No contact found for company '{company.Name}'. Add a contact first or pass recipientEmail.");
        }

        // Enrich the recipient contact with name/notes captured in the apply panel.
        if (!string.IsNullOrWhiteSpace(dto.RecipientName)) contact.Name = dto.RecipientName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ContactNotes)) contact.Notes = dto.ContactNotes.Trim();

        // Render subject/body from template
        var subject = TemplateVariableResolver.Render(template.SubjectTemplate, Default(template), company, user);
        var body = TemplateVariableResolver.Render(template.BodyTemplate, Default(template), company, user);

        // Application (SAVED) — every apply is tracked
        var app = await _applications.CreateAsync(new CreateApplicationDto(
            CandidateId: userId,
            CvVersionId: template.CvVersionId,
            JobOfferId: null,
            CompanyName: company.Name,
            PositionTitle: dto.ScheduleName ?? "Application",
            OfferSource: "generic_schedule",
            Notes: null,
            Origin: "MANUAL",
            Status: "SAVED",
            AllowDuplicate: true
        ), userId);

        // Scheduled attempt (does not transition status until actually sent)
        await _applications.CreateAttemptAsync(app.Id, new CreateAttemptDto(
            Channel: "EMAIL_GMAIL",
            InitiatedBy: "SCHEDULE",
            Status: "SCHEDULED",
            Subject: subject,
            Body: body,
            ContactId: contact.Id,
            CvVersionId: template.CvVersionId,
            SentAt: null
        ), userId);

        // Concrete per-company schedule
        var schedule = new EmailSchedule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.ScheduleName ?? $"{template.Name} → {company.Name}",
            Subject = subject,
            Body = body,
            CronExpression = dto.CronExpression,
            RecipientIds = [contact.Id],
            ApplicationId = app.Id,
            CvVersionId = template.CvVersionId,
            TemplateSourceId = template.Id,
            AttachmentRefsJson = template.AttachmentRefsJson,
            NextRunAt = ComputeNextRun(dto.CronExpression)
        };
        _db.Set<EmailSchedule>().Add(schedule);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Applied template {Template} to company {Company} → schedule {Schedule}", template.Id, company.Name, schedule.Id);

        return new ApplyTemplateResultDto
        {
            ScheduleId = schedule.Id,
            ApplicationId = app.Id,
            CompanyId = company.Id,
            ContactId = contact.Id
        };
    }

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
            return (-1, 0);
        }

        if (contacts.Count == 0)
        {
            _logger.LogWarning("Schedule {Name} ({Id}) has no valid recipients — parking it", schedule.Name, schedule.Id);
            schedule.LastRunAt = now;
            schedule.NextRunAt = null;
            schedule.UpdatedAt = now;
            await _db.SaveChangesAsync();
            return (0, 0);
        }

        var cvPdfUrl = await ResolveCvPdfUrlAsync(schedule.CvVersionId);
        var extraAttachments = await BuildFireAttachmentsAsync(schedule.AttachmentRefsJson);

        int sent = 0, failed = 0;

        foreach (var contact in contacts)
        {
            try
            {
                await _gmail.SendWithAttachmentAsync(userId, contact.Email, schedule.Subject, schedule.Body, cvPdfUrl, extraAttachments);

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

                // Log a SENT attempt on the linked application (closes the apply tracking loop).
                if (schedule.ApplicationId.HasValue)
                {
                    try
                    {
                        await _applications.CreateAttemptAsync(schedule.ApplicationId.Value, new CreateAttemptDto(
                            Channel: "EMAIL_GMAIL",
                            InitiatedBy: "SCHEDULE",
                            Status: "SENT",
                            Subject: schedule.Subject,
                            Body: schedule.Body,
                            ContactId: contact.Id,
                            CvVersionId: schedule.CvVersionId,
                            SentAt: now
                        ), userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Schedule {Id} sent but attempt logging failed for application {App}", schedule.Id, schedule.ApplicationId);
                    }
                }

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

    private ScheduleVariableDefaults? Default(ScheduleTemplate t)
        => string.IsNullOrWhiteSpace(t.VariableDefaultsJson)
            ? null
            : JsonSerializer.Deserialize<ScheduleVariableDefaults>(t.VariableDefaultsJson);

    private async Task<Contact?> ResolveContactForCompanyAsync(Guid userId, Company company, string? recipientEmail)
    {
        if (!string.IsNullOrWhiteSpace(recipientEmail))
        {
            var email = recipientEmail!.Trim().ToLower();
            var existing = await _db.Contacts.FirstOrDefaultAsync(c => c.UserId == userId && c.Email == email);
            if (existing is not null) return existing;

            var created = new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = company.Name,
                Email = email,
                Company = company.Name,
                Source = "schedule_apply",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Contacts.Add(created);
            await _db.SaveChangesAsync();
            return created;
        }

        var byCompany = await _db.Contacts
            .Where(c => c.UserId == userId && c.Company != null && c.Company.ToLower() == company.Name.ToLower())
            .OrderByDescending(c => c.IsFavorite)
            .FirstOrDefaultAsync();
        return byCompany;
    }

    private async Task<string?> ResolveCvPdfUrlAsync(Guid? cvVersionId)
    {
        if (cvVersionId is null) return null;
        var cv = await _db.CvVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == cvVersionId.Value);
        return cv?.PdfUrl;
    }

    private async Task<List<CV_Generator.Dto.EmailAttachmentDto>> BuildFireAttachmentsAsync(string? refsJson)
    {
        if (string.IsNullOrWhiteSpace(refsJson)) return [];
        List<ScheduleAttachmentRef>? refs = null;
        try { refs = JsonSerializer.Deserialize<List<ScheduleAttachmentRef>>(refsJson); }
        catch { return []; }
        if (refs is null || refs.Count == 0) return [];

        var result = new List<CV_Generator.Dto.EmailAttachmentDto>();
        foreach (var attachmentRef in refs)
        {
            try
            {
                var bytes = await _minio.GetObjectAsync(AttachmentBucket, attachmentRef.ObjectKey);
                result.Add(new CV_Generator.Dto.EmailAttachmentDto
                {
                    FileName = attachmentRef.FileName,
                    ContentType = attachmentRef.ContentType ?? "application/octet-stream",
                    ContentBase64 = Convert.ToBase64String(bytes)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load attachment {Key} for schedule fire", attachmentRef.ObjectKey);
            }
        }
        return result;
    }

    private static string? SerializeRefs(List<ScheduleAttachmentRef>? refs)
        => refs is { Count: > 0 } ? JsonSerializer.Serialize(refs) : null;

    internal static DateTime? ComputeNextRun(string cron)
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
        ApplicationId = s.ApplicationId,
        CvVersionId = s.CvVersionId,
        TemplateSourceId = s.TemplateSourceId,
        Attachments = DeserializeRefs(s.AttachmentRefsJson),
        CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt
    };

    private static List<ScheduleAttachmentRef> DeserializeRefs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<ScheduleAttachmentRef>>(json) ?? []; }
        catch { return []; }
    }

    internal static string? ValidateCron(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron)) return "Cron expression is required";
        try
        {
            Cronos.CronExpression.Parse(cron);
            return null;
        }
        catch (Exception)
        {
            return $"'{cron}' is not a valid cron expression";
        }
    }
}