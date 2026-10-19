using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;
using CV_Generator;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ApplicationsController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMinioStorageService _minio;
    private readonly IGmailSendService _gmailSend;
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public ApplicationsController(
        IApplicationService service,
        ICurrentUserService currentUser,
        ILogger<ApplicationsController> logger,
        IServiceScopeFactory scopeFactory,
        IMinioStorageService minio,
        IGmailSendService gmailSend,
        AppDbContext db,
        IHttpClientFactory httpClientFactory)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _minio = minio;
        _gmailSend = gmailSend;
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    private Guid? UserId => _currentUser.UserId;

    /// GET /applications
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? statuses = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? appliedFrom = null,
        [FromQuery] DateTime? appliedTo = null,
        [FromQuery] DateTime? updatedFrom = null,
        [FromQuery] DateTime? updatedTo = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 20;

        var statusArr = !string.IsNullOrWhiteSpace(statuses)
            ? statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

        var result = await _service.GetAllAsync(UserId.Value, page, pageSize, statusArr, search,
            appliedFrom, appliedTo, updatedFrom, updatedTo, sortBy, sortDir);
        return Ok(ApiResponse<ApplicationListDto>.Ok(result));
    }

    /// GET /applications/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var app = await _service.GetByIdAsync(id, UserId.Value);
        if (app == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
        return Ok(ApiResponse<ApplicationResponseDto>.Ok(app));
    }

    /// POST /applications/check-duplicate
    [HttpPost("check-duplicate")]
    public async Task<IActionResult> CheckDuplicate([FromBody] DuplicateCheckRequestDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        if (string.IsNullOrWhiteSpace(dto.CompanyName) || string.IsNullOrWhiteSpace(dto.PositionTitle))
            return BadRequest(ApiResponse<DuplicateCheckResponseDto>.Error("CompanyName and PositionTitle are required"));

        var result = await _service.CheckDuplicatesAsync(UserId.Value, dto);
        return Ok(ApiResponse<DuplicateCheckResponseDto>.Ok(result));
    }

    /// POST /applications
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        try
        {
            var created = await _service.CreateAsync(dto, UserId.Value);
            SearchSyncHelper.TriggerSync(_scopeFactory, UserId.Value, _logger, "Application.Create");
            return Created($"/api/applications/{created.Id}", ApiResponse<ApplicationResponseDto>.Created(created));
        }
        catch (DuplicateApplicationException ex)
        {
            return Conflict(ApiResponse<ApplicationResponseDto>.Error(ex.Message, ex.Payload));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(ex.Message));
        }
    }

    /// PATCH /applications/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        try
        {
            var updated = await _service.UpdateStatusAsync(id, dto, UserId.Value);
            if (updated == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
            return Ok(ApiResponse<ApplicationResponseDto>.Ok(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(ex.Message));
        }
    }

    /// PATCH /applications/{id}/follow-up
    /// Decision from a follow-up reminder: REJECTED / ACCEPTED / INTERVIEW (status change)
    /// or KEEP (dismiss reminder without changing status).
    [HttpPatch("{id}/follow-up")]
    public async Task<IActionResult> ActionFollowUp(Guid id, [FromBody] FollowUpActionDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        try
        {
            var updated = await _service.ApplyFollowUpActionAsync(id, dto, UserId.Value);
            if (updated == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
            SearchSyncHelper.TriggerSync(_scopeFactory, UserId.Value, _logger, "Application.Update");
            return Ok(ApiResponse<ApplicationResponseDto>.Ok(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(ex.Message));
        }
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApplicationDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        try
        {
            var updated = await _service.UpdateDetailsAsync(id, dto, UserId.Value);
            if (updated == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
            SearchSyncHelper.TriggerSync(_scopeFactory, UserId.Value, _logger, "Application.Update");
            return Ok(ApiResponse<ApplicationResponseDto>.Ok(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(ex.Message));
        }
    }

    /// DELETE /applications/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var deleted = await _service.DeleteAsync(id, UserId.Value);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Application not found"));
        SearchSyncHelper.TriggerSync(_scopeFactory, UserId.Value, _logger, "Application.Delete");
        return NoContent();
    }

    /// GET /applications/statistics
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        var stats = await _service.GetStatisticsAsync(UserId.Value);
        return Ok(ApiResponse<ApplicationStatisticsDto>.Ok(stats));
    }

    /// GET /applications/statistics/trends
    [HttpGet("statistics/trends")]
    public async Task<IActionResult> GetTrends()
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        var trends = await _service.GetTrendsAsync(UserId.Value);
        return Ok(ApiResponse<StatisticsTrendsDto>.Ok(trends));
    }

    /// GET /applications/analytics/summary
    [HttpGet("analytics/summary")]
    public async Task<IActionResult> GetAnalyticsSummary()
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        var summary = await _service.GetAnalyticsSummaryAsync(UserId.Value);
        return Ok(ApiResponse<AnalyticsSummaryDto>.Ok(summary));
    }

    /// PATCH /applications/{id}/toggle-save
    [HttpPatch("{id}/toggle-save")]
    public async Task<IActionResult> ToggleSave(Guid id)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var isSaved = await _service.ToggleSaveAsync(id, UserId.Value);
        if (isSaved == null) return NotFound(ApiResponse<bool>.Error("Application not found"));
        return Ok(ApiResponse<bool>.Ok(isSaved.Value));
    }

    /// GET /applications/calendar-events
    [HttpGet("calendar-events")]
    public async Task<IActionResult> GetCalendarEvents(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? statuses = null)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var statusArr = !string.IsNullOrWhiteSpace(statuses)
            ? statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

        var events = await _service.GetCalendarEventsAsync(UserId.Value, from, to, statusArr);
        return Ok(ApiResponse<List<CalendarEventDto>>.Ok(events));
    }

    /// GET /applications/activity
    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] int limit = 50)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        var feed = await _service.GetActivityFeedAsync(UserId.Value, limit);
        return Ok(ApiResponse<ActivityFeedDto>.Ok(feed));
    }

    /// GET /applications/{id}/attempts
    [HttpGet("{id}/attempts")]
    public async Task<IActionResult> GetAttempts(Guid id)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        try
        {
            var attempts = await _service.GetAttemptsAsync(id, UserId.Value);
            return Ok(ApiResponse<List<AttemptResponseDto>>.Ok(attempts));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<List<AttemptResponseDto>>.Error("Application not found"));
        }
    }

    /// GET /applications/{id}/suggested-contacts — contacts matching the application's company
    [HttpGet("{id}/suggested-contacts")]
    public async Task<IActionResult> SuggestedContacts(Guid id)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        var suggestions = await _service.GetSuggestedContactsAsync(id, UserId.Value);
        return Ok(ApiResponse<List<ContactSummaryDto>>.Ok(suggestions));
    }

    /// POST /applications/{id}/attempts
    [HttpPost("{id}/attempts")]
    public async Task<IActionResult> CreateAttempt(Guid id, [FromBody] CreateAttemptDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        try
        {
            var created = await _service.CreateAttemptAsync(id, dto, UserId.Value);
            return Created($"/api/applications/{id}/attempts/{created.Id}", ApiResponse<AttemptResponseDto>.Created(created));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<AttemptResponseDto>.Error("Application not found"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<AttemptResponseDto>.Error(ex.Message));
        }
    }

    /// GET /applications/{id}/attachments/{index}/download — streams an attachment's bytes
    [HttpGet("{id}/attachments/{index}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid id, int index)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var att = await _service.GetAttachmentAsync(id, index, UserId.Value);
        if (att == null || !att.ObjectKey.StartsWith($"{UserId.Value}/", StringComparison.Ordinal))
            return NotFound(ApiResponse<object>.Error("Attachment not found"));

        // Display name: objectKey = "{userId}/{guid}_{originalName}" → strip "{guid}_" prefix.
        var keyName = att.ObjectKey[(att.ObjectKey.LastIndexOf('/') + 1)..];
        string displayName;
        var underscore = keyName.IndexOf('_');
        displayName = underscore > 0 && underscore < keyName.Length - 1 ? keyName[(underscore + 1)..] : keyName;

        try
        {
            var bytes = await _minio.GetObjectAsync("mail-attachments", att.ObjectKey);
            if (bytes.Length == 0) return NotFound(ApiResponse<object>.Error("Attachment not found"));
            return File(bytes, "application/octet-stream", displayName);
        }
        catch
        {
            return NotFound(ApiResponse<object>.Error("Attachment not found"));
        }
    }

    /// DELETE /applications/{id}/attachments/{index} — detaches without deleting the MinIO object
    [HttpDelete("{id}/attachments/{index}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, int index)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var updated = await _service.RemoveAttachmentAsync(id, index, UserId.Value);
        if (updated == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application or attachment not found"));
        return Ok(ApiResponse<ApplicationResponseDto>.Ok(updated));
    }

    /// GET /applications/{id}/attempts/{attemptId}/attachments/{index}/download — streams an attempt attachment
    [HttpGet("{id}/attempts/{attemptId}/attachments/{index}/download")]
    public async Task<IActionResult> DownloadAttemptAttachment(Guid id, Guid attemptId, int index)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var att = await _service.GetAttemptAttachmentAsync(id, attemptId, index, UserId.Value);
        if (att == null || !att.ObjectKey.StartsWith($"{UserId.Value}/", StringComparison.Ordinal))
            return NotFound(ApiResponse<object>.Error("Attachment not found"));

        var keyName = att.ObjectKey[(att.ObjectKey.LastIndexOf('/') + 1)..];
        var underscore = keyName.IndexOf('_');
        var displayName = underscore > 0 && underscore < keyName.Length - 1 ? keyName[(underscore + 1)..] : keyName;

        try
        {
            var bytes = await _minio.GetObjectAsync("mail-attachments", att.ObjectKey);
            if (bytes.Length == 0) return NotFound(ApiResponse<object>.Error("Attachment not found"));
            return File(bytes, "application/octet-stream", displayName);
        }
        catch
        {
            return NotFound(ApiResponse<object>.Error("Attachment not found"));
        }
    }

    /// PATCH /applications/{id}/attempts/{attemptId}
    [HttpPatch("{id}/attempts/{attemptId}")]
    public async Task<IActionResult> UpdateAttempt(Guid id, Guid attemptId, [FromBody] UpdateAttemptDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        try
        {
            var updated = await _service.UpdateAttemptAsync(id, attemptId, dto, UserId.Value);
            if (updated == null) return NotFound(ApiResponse<AttemptResponseDto>.Error("Attempt not found"));
            return Ok(ApiResponse<AttemptResponseDto>.Ok(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<AttemptResponseDto>.Error("Application not found"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<AttemptResponseDto>.Error(ex.Message));
        }
    }

    /// POST /applications/{id}/attempts/send — sends the composed email through the
    /// user's Gmail and logs it as a SENT attempt (with the Gmail message/thread ids).
    [HttpPost("{id}/attempts/send")]
    public async Task<IActionResult> SendAttempt(Guid id, [FromBody] SendAttemptDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        if (string.IsNullOrWhiteSpace(dto.RecipientContact))
            return BadRequest(ApiResponse<AttemptResponseDto>.Error("A recipient email is required to send"));

        // Read each attached file's bytes from MinIO (object keys are owner-prefixed).
        var sendAttachments = new List<EmailAttachmentDto>();
        foreach (var att in dto.Attachments ?? [])
        {
            if (string.IsNullOrWhiteSpace(att.ObjectKey) ||
                !att.ObjectKey.StartsWith($"{UserId.Value}/", StringComparison.Ordinal))
                return BadRequest(ApiResponse<AttemptResponseDto>.Error($"Attachment '{att.FileName}' not found"));

            byte[] bytes;
            try { bytes = await _minio.GetObjectAsync("mail-attachments", att.ObjectKey); }
            catch { bytes = []; }

            if (bytes.Length == 0)
                return BadRequest(ApiResponse<AttemptResponseDto>.Error($"Attachment '{att.FileName}' not found"));

            sendAttachments.Add(new EmailAttachmentDto
            {
                FileName = att.FileName,
                ContentType = string.IsNullOrWhiteSpace(att.ContentType) ? "application/octet-stream" : att.ContentType,
                ContentBase64 = Convert.ToBase64String(bytes)
            });
        }

        var connection = await _db.Set<GmailConnection>()
            .FirstOrDefaultAsync(c => c.UserId == UserId.Value && !c.IsRevoked);
        var fromEmail = connection?.GmailAddress ?? "noreply@propel.com";
        var provider = connection is not null ? "gmail" : "smtp";

        // Resolve the CV version chosen in the modal so it is ACTUALLY attached to the sent email
        // (ownership-checked). Without this the attempt merely recorded the CV id.
        var (cvPdfUrl, cvTitle) = await ResolveCvPdfUrlAsync(dto.CvVersionId);
        cvPdfUrl = await SkipDuplicateCvAsync(cvPdfUrl, sendAttachments);

        // Same for the cover letter version — attached as an extra file when one was selected.
        await AddCoverLetterAttachmentAsync(dto.CoverLetterVersionId, sendAttachments);

        string? messageId = null;
        string? threadId = null;
        try
        {
            (messageId, threadId) = await _gmailSend.SendWithAttachmentAsync(
                UserId.Value, dto.RecipientContact, dto.Subject ?? string.Empty, dto.Body ?? string.Empty,
                cvPdfUrl, cvTitle, sendAttachments.Count > 0 ? sendAttachments : null);
        }
        catch (GmailReauthRequiredException ex)
        {
            _logger.LogWarning(ex, "Gmail re-auth required while sending attempt on application {AppId}", id);
            return StatusCode(StatusCodes.Status502BadGateway, ApiResponse<AttemptResponseDto>.Error(
                "Gmail session expired — reconnect Gmail in the Mailbox, then try again."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail send failed for application {AppId} user {UserId}", id, UserId.Value);
            return StatusCode(StatusCodes.Status502BadGateway, ApiResponse<AttemptResponseDto>.Error(
                $"Gmail send failed: {ex.Message}"));
        }

        // Record the message in mailbox history (best-effort — the email already left).
        try
        {
            _db.Set<EmailMessage>().Add(new EmailMessage
            {
                Id = Guid.NewGuid(),
                UserId = UserId.Value,
                ContactId = dto.ContactId,
                FromEmail = fromEmail,
                ToEmail = dto.RecipientContact,
                ToName = dto.RecipientName ?? string.Empty,
                Subject = dto.Subject ?? string.Empty,
                Body = dto.Body ?? string.Empty,
                Status = "sent",
                Provider = provider,
                SentAt = DateTime.UtcNow,
                AttachmentMetadataJson = dto.Attachments is { Count: > 0 }
                    ? JsonSerializer.Serialize(dto.Attachments.Select(a => new EmailAttachmentInfoDto
                    {
                        FileName = a.FileName,
                        ContentType = a.ContentType ?? "application/octet-stream",
                        SizeBytes = a.SizeBytes
                    }).ToList())
                    : null
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record mailbox history for attempt send on application {AppId}", id);
        }

        try
        {
            var attempt = await _service.CreateAttemptAsync(id, new CreateAttemptDto(
                Channel: "EMAIL_GMAIL",
                InitiatedBy: "USER",
                Status: "SENT",
                Subject: dto.Subject,
                Body: dto.Body,
                RecipientName: dto.RecipientName,
                RecipientContact: dto.RecipientContact,
                ContactId: dto.ContactId,
                ChannelMetadataJson: messageId != null || threadId != null
                    ? JsonSerializer.Serialize(new { messageId, threadId })
                    : null,
                CvVersionId: dto.CvVersionId,
                CoverLetterVersionId: dto.CoverLetterVersionId,
                Attachments: dto.Attachments,
                SentAt: DateTime.UtcNow), UserId.Value);

            return Ok(ApiResponse<AttemptResponseDto>.Ok(attempt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email sent but attempt logging failed on application {AppId}", id);
            return StatusCode(StatusCodes.Status502BadGateway, ApiResponse<AttemptResponseDto>.Error(
                "Email was sent, but the attempt could not be logged. Reload and check the timeline."));
        }
    }

    /// <summary>Resolves the selected CV version's PDF URL + title (ownership-checked).</summary>
    private async Task<(string? PdfUrl, string? Title)> ResolveCvPdfUrlAsync(Guid? cvVersionId)
    {
        if (cvVersionId is null || UserId is null) return (null, null);
        var cv = await _db.CvVersions.AsNoTracking()
            .Include(v => v.Cv)
            .FirstOrDefaultAsync(v => v.Id == cvVersionId.Value && v.Cv.UserId == UserId.Value);
        return (cv?.PdfUrl, cv?.Cv.Title);
    }

    /// <summary>
    /// If one of the explicit attachments is byte-identical to the auto-attached CV, drop the
    /// auto CV so the recipient doesn't receive the same file twice.
    /// </summary>
    private async Task<string?> SkipDuplicateCvAsync(string? cvPdfUrl, List<EmailAttachmentDto> attachments)
    {
        if (cvPdfUrl is null || attachments.Count == 0) return cvPdfUrl;
        try
        {
            using var http = _httpClientFactory.CreateClient();
            var cvBytes = await http.GetByteArrayAsync(cvPdfUrl);
            var cvDigest = SHA256.HashData(cvBytes);
            foreach (var att in attachments)
            {
                if (string.IsNullOrWhiteSpace(att.ContentBase64)) continue;
                try
                {
                    if (cvDigest.AsSpan().SequenceEqual(SHA256.HashData(Convert.FromBase64String(att.ContentBase64))))
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

    /// <summary>
    /// Resolves the selected cover-letter version (ownership-checked) and appends its file to the
    /// outgoing attachments, skipping it when a byte-identical file is already attached.
    /// </summary>
    private async Task AddCoverLetterAttachmentAsync(Guid? coverLetterVersionId, List<EmailAttachmentDto> attachments)
    {
        if (coverLetterVersionId is null || UserId is null) return;

        var version = await _db.CoverLetterVersions.AsNoTracking()
            .Include(v => v.CoverLetter)
            .FirstOrDefaultAsync(v => v.Id == coverLetterVersionId.Value && v.CoverLetter.UserId == UserId.Value);
        if (version is null) return;

        var isPdf = !string.IsNullOrWhiteSpace(version.PdfUrl);
        var url = version.PdfUrl ?? version.FileUrl;
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var bytes = await http.GetByteArrayAsync(url);
            if (bytes.Length == 0) return;

            // Don't send the same file twice if it was already added as an explicit attachment.
            var digest = SHA256.HashData(bytes);
            foreach (var att in attachments)
            {
                if (string.IsNullOrWhiteSpace(att.ContentBase64)) continue;
                try
                {
                    if (digest.AsSpan().SequenceEqual(SHA256.HashData(Convert.FromBase64String(att.ContentBase64))))
                        return;
                }
                catch (FormatException) { /* skip unreadable attachment */ }
            }

            var ext = isPdf ? ".pdf"
                : Path.GetExtension(url) is { Length: > 0 } e ? e : ".pdf";
            attachments.Add(new EmailAttachmentDto
            {
                FileName = SanitizeFileName(version.CoverLetter.Title) + ext,
                ContentType = isPdf ? "application/pdf" : "application/octet-stream",
                ContentBase64 = Convert.ToBase64String(bytes)
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to attach cover letter version {VersionId}; sending without it", coverLetterVersionId);
        }
    }

    private static string SanitizeFileName(string? name)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "cover-letter" : name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(c, '_');
        return baseName;
    }
}
