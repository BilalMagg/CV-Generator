using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

/// <summary>
/// CRUD for reusable "email draft" compositions saved from the Apply wizard. Attachments are
/// stored in MinIO (<see cref="AttachmentBucket"/>) and referenced via <see cref="ScheduleAttachmentRef"/>;
/// <see cref="EmailDraft.CvTitle"/> is denormalized from the CvVersion → Cv chain for list display.
/// </summary>
public class EmailDraftService
{
    private readonly AppDbContext _db;
    private readonly IMinioStorageService _minio;
    private readonly ILogger<EmailDraftService> _logger;
    private const string AttachmentBucket = "mail-attachments";

    public EmailDraftService(AppDbContext db, IMinioStorageService minio, ILogger<EmailDraftService> logger)
    {
        _db = db;
        _minio = minio;
        _logger = logger;
    }

    public async Task<List<EmailDraftListItemDto>> ListAsync(Guid userId)
    {
        var drafts = await _db.Set<EmailDraft>()
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();
        return drafts.Select(d => new EmailDraftListItemDto
        {
            Id = d.Id,
            Name = d.Name,
            CompanyName = d.CompanyName,
            PositionTitle = d.PositionTitle,
            RecipientEmail = d.RecipientEmail,
            Subject = d.Subject,
            UpdatedAt = d.UpdatedAt
        }).ToList();
    }

    public async Task<EmailDraftDto?> GetAsync(Guid id, Guid userId)
    {
        var draft = await _db.Set<EmailDraft>().AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        return draft is null ? null : ToDto(draft);
    }

    public async Task<EmailDraftDto> CreateAsync(Guid userId, CreateEmailDraftDto dto)
    {
        var cvTitle = await ResolveCvTitleAsync(dto.CvVersionId);
        var refs = await UploadAttachmentsAsync(userId, dto.Attachments);

        var draft = new EmailDraft
        {
            UserId = userId,
            Name = dto.Name.Trim(),
            CompanyName = NormalizeNull(dto.CompanyName),
            PositionTitle = NormalizeNull(dto.PositionTitle),
            CompanyDescription = NormalizeNull(dto.CompanyDescription),
            RecipientEmail = NormalizeNull(dto.RecipientEmail),
            RecipientName = NormalizeNull(dto.RecipientName),
            ContactNotes = NormalizeNull(dto.ContactNotes),
            Subject = NormalizeNull(dto.Subject),
            Body = NormalizeNull(dto.Body),
            CvVersionId = dto.CvVersionId is null || dto.CvVersionId == Guid.Empty ? null : dto.CvVersionId,
            CvTitle = cvTitle,
            AttachmentsJson = refs.Count > 0 ? JsonSerializer.Serialize(refs) : null
        };
        _db.Set<EmailDraft>().Add(draft);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Email draft {Draft} saved for user {User}", draft.Id, userId);
        return ToDto(draft);
    }

    public async Task<EmailDraftDto?> UpdateAsync(Guid id, Guid userId, UpdateEmailDraftDto dto)
    {
        var draft = await _db.Set<EmailDraft>().FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (draft is null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Name)) draft.Name = dto.Name.Trim();
        if (dto.CompanyName is not null) draft.CompanyName = NormalizeNull(dto.CompanyName);
        if (dto.PositionTitle is not null) draft.PositionTitle = NormalizeNull(dto.PositionTitle);
        if (dto.CompanyDescription is not null) draft.CompanyDescription = NormalizeNull(dto.CompanyDescription);
        if (dto.RecipientEmail is not null) draft.RecipientEmail = NormalizeNull(dto.RecipientEmail);
        if (dto.RecipientName is not null) draft.RecipientName = NormalizeNull(dto.RecipientName);
        if (dto.ContactNotes is not null) draft.ContactNotes = NormalizeNull(dto.ContactNotes);
        if (dto.Subject is not null) draft.Subject = NormalizeNull(dto.Subject);
        if (dto.Body is not null) draft.Body = NormalizeNull(dto.Body);

        if (dto.CvVersionId.HasValue)
        {
            var cvId = dto.CvVersionId == Guid.Empty ? null : dto.CvVersionId;
            draft.CvVersionId = cvId;
            draft.CvTitle = cvId is null ? null : await ResolveCvTitleAsync(cvId);
        }

        if (dto.Attachments is not null)
        {
            await DeleteAttachmentsAsync(draft.AttachmentsJson);
            var refs = await UploadAttachmentsAsync(userId, dto.Attachments.Count > 0 ? dto.Attachments : null);
            draft.AttachmentsJson = refs.Count > 0 ? JsonSerializer.Serialize(refs) : null;
        }

        draft.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(draft);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var draft = await _db.Set<EmailDraft>().FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (draft is null) return false;

        await DeleteAttachmentsAsync(draft.AttachmentsJson);
        _db.Set<EmailDraft>().Remove(draft);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Email draft {Draft} deleted for user {User}", id, userId);
        return true;
    }

    /// <summary>Fetches a single attachment ref (ownership-checked by draft).
    /// The MinIO object itself must still be guarded by the caller.</summary>
    public async Task<ScheduleAttachmentRef?> GetAttachmentAsync(Guid id, int index, Guid userId)
    {
        var draft = await _db.Set<EmailDraft>().AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (draft is null) return null;
        var attachments = DeserializeAttachments(draft.AttachmentsJson);
        if (attachments is null || index < 0 || index >= attachments.Count) return null;
        return attachments[index];
    }

    private EmailDraftDto ToDto(EmailDraft d)
    {
        var attachments = DeserializeAttachments(d.AttachmentsJson);
        return new EmailDraftDto
        {
            Id = d.Id,
            Name = d.Name,
            CompanyName = d.CompanyName,
            PositionTitle = d.PositionTitle,
            CompanyDescription = d.CompanyDescription,
            RecipientEmail = d.RecipientEmail,
            RecipientName = d.RecipientName,
            ContactNotes = d.ContactNotes,
            Subject = d.Subject,
            Body = d.Body,
            CvVersionId = d.CvVersionId,
            CvTitle = d.CvTitle,
            Attachments = attachments ?? [],
            HasCv = d.CvVersionId is not null,
            HasAttachments = attachments is { Count: > 0 },
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        };
    }

    private async Task<string?> ResolveCvTitleAsync(Guid? cvVersionId)
    {
        if (cvVersionId is null || cvVersionId == Guid.Empty) return null;
        var cv = await _db.CvVersions.AsNoTracking()
            .Include(v => v.Cv)
            .FirstOrDefaultAsync(v => v.Id == cvVersionId.Value);
        return cv?.Cv.Title;
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
                _logger.LogWarning(ex, "Failed to upload attachment {File} for email draft", att.FileName);
            }
        }
        return refs;
    }

    private async Task DeleteAttachmentsAsync(string? attachmentsJson)
    {
        var refs = DeserializeAttachments(attachmentsJson);
        if (refs is null) return;
        foreach (var att in refs)
        {
            try
            {
                await _minio.DeleteAsync(AttachmentBucket, att.ObjectKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete email-draft attachment object {Key}", att.ObjectKey);
            }
        }
    }

    private static List<ScheduleAttachmentRef>? DeserializeAttachments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<ScheduleAttachmentRef>>(json); }
        catch { return null; }
    }

    private static string? NormalizeNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}