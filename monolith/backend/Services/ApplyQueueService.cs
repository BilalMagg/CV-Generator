using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

/// <summary>
/// The Apply Queue: a staging container where composed applies (from the bulk-apply wizard or
/// the quick-apply wizard) are parked instead of being sent directly. Queueing creates the
/// tracking shell immediately — Company + optional Contact + a SAVED Application, so the
/// pending apply is visible in the kanban and analytics. Executing a selection builds a real
/// <see cref="BulkApplyRun"/> (reusing the proven sequential send pipeline); each run row keeps
/// a link back to its queue item and mirrors its terminal status onto it.
/// </summary>
public class ApplyQueueService
{
    private readonly AppDbContext _db;
    private readonly ApplyService _apply;
    private readonly BulkApplyService _bulk;
    private readonly IApplicationService _applications;
    private readonly ILogger<ApplyQueueService> _logger;

    public ApplyQueueService(
        AppDbContext db,
        ApplyService apply,
        BulkApplyService bulk,
        IApplicationService applications,
        ILogger<ApplyQueueService> logger)
    {
        _db = db;
        _apply = apply;
        _bulk = bulk;
        _applications = applications;
        _logger = logger;
    }

    /// <summary>Creates the tracking shell (Company + optional Contact + SAVED Application) and
    /// stores the queued item referencing it. Tokens in Subject/Body are expected to be already
    /// resolved client-side; the send path renders them again harmlessly.</summary>
    public async Task<ApplyQueueItemDto> CreateItemAsync(Guid userId, CreateApplyQueueItemDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CompanyName)) throw new ArgumentException("CompanyName is required");
        if (string.IsNullOrWhiteSpace(dto.RecipientEmail)) throw new ArgumentException("RecipientEmail is required");

        // Same AllowDuplicate:true semantics as bulk sends — the queue is a staging area and a
        // duplicate application would block parking the apply, defeating the purpose.
        var company = await _apply.UpsertCompanyAsync(userId, dto.CompanyName.Trim(), dto.CompanyDescription);
        var contact = await _apply.UpsertContactAsync(userId, company, dto.RecipientEmail.Trim(), dto.RecipientName, dto.ContactNotes);

        var source = dto.Source == "bulk_apply" ? "bulk_apply" : "job_post_apply";
        var app = await _applications.CreateAsync(new CreateApplicationDto(
            CandidateId: userId,
            CvVersionId: dto.CvVersionId,
            JobOfferId: null,
            CompanyName: company.Name,
            PositionTitle: (dto.PositionTitle ?? string.Empty).Trim(),
            OfferSource: source,
            Notes: null,
            Origin: "MANUAL",
            Status: "SAVED",
            AllowDuplicate: true
        ), userId);

        var item = new ApplyQueueItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Source = dto.Source == "bulk_apply" ? "bulk_apply" : "quick_apply",
            ApplicationId = app.Id,
            CompanyId = company.Id,
            ContactId = contact.Id,
            CompanyName = company.Name,
            Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim(),
            PositionTitle = string.IsNullOrWhiteSpace(dto.PositionTitle) ? null : dto.PositionTitle.Trim(),
            RecipientEmail = dto.RecipientEmail.Trim(),
            RecipientName = string.IsNullOrWhiteSpace(dto.RecipientName) ? null : dto.RecipientName.Trim(),
            Subject = (dto.Subject ?? string.Empty).Trim(),
            Body = dto.Body ?? string.Empty,
            CvVersionId = dto.CvVersionId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        _db.ApplyQueueItems.Add(item);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Apply queue: staged item for {Company} <{Email}> (application {App})",
            item.CompanyName, item.RecipientEmail, app.Id);
        return Map(item, app.Status.ToString());
    }

    /// <summary>Same as <see cref="CreateItemAsync"/> for a whole batch (bulk-apply staging).</summary>
    public async Task<List<ApplyQueueItemDto>> CreateItemsAsync(Guid userId, IReadOnlyList<CreateApplyQueueItemDto> dtos)
    {
        var result = new List<ApplyQueueItemDto>(dtos.Count);
        foreach (var dto in dtos)
        {
            try
            {
                result.Add(await CreateItemAsync(userId, dto));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Apply queue batch: skipped item for {Company}", dto.CompanyName);
            }
        }
        return result;
    }

    /// <summary>Lists the user's queue items, newest first. When a status filter is supplied
    /// only matching items are returned; "pending" (the default view) is the standard screen.</summary>
    public async Task<List<ApplyQueueItemDto>> ListAsync(Guid userId, string? status = null)
    {
        var query = _db.ApplyQueueItems
            .AsNoTracking()
            .Include(i => i.Application)
            .Where(i => i.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            query = query.Where(i => i.Status == status.ToLower());

        var items = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
        return items.Select(i => Map(i, i.Application?.Status.ToString())).ToList();
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id)
    {
        var item = await _db.ApplyQueueItems
            .Include(i => i.Application)
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);
        if (item is null) return false;

        // The linked application exists only for THIS apply, so when it never progressed past
        // SAVED (no attempts, no profile data) deleting the queue item would leave a SAVED card
        // nobody sent — remove it together with the item. Applications that already moved on
        // (sent attempt, status changed) are real tracking records and always stay.
        var attempts = await _db.ApplicationAttempts.CountAsync(a => a.ApplicationId == item.ApplicationId);
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == item.ApplicationId);
        if (app is { Status: ApplicationStatus.SAVED } && attempts == 0)
            _db.Applications.Remove(app);

        _db.ApplyQueueItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Executes a selection of queue items as a bulk-apply run. Rows are built from the items
    /// (per-item CV fallback to the requested run-level CV) and each row is linked back to its
    /// item. Items are flipped to "queued" so they leave the pending view immediately; their
    /// final status (sent/failed) is mirrored by the bulk executor.
    /// </summary>
    public async Task<ExecuteApplyQueueResult> ExecuteAsync(Guid userId, ExecuteApplyQueueDto dto)
    {
        if (dto.ItemIds.Count == 0) throw new ArgumentException("Select at least one queued apply");
        if (dto.ItemIds.Count > BulkApplyService.MaxRows)
            throw new ArgumentException($"Too many items (maximum {BulkApplyService.MaxRows} per run)");

        var items = await _db.ApplyQueueItems
            .Where(i => i.UserId == userId && dto.ItemIds.Contains(i.Id))
            .ToListAsync();
        if (items.Count == 0) throw new ArgumentException("No matching queue items");

        // Items already consumed by another (unfinished?) run — ids are new per create so this
        // is defensive only.
        var usable = items.Where(i => i.Status == "pending" || i.Status == "failed").ToList();
        if (usable.Count == 0)
            throw new ArgumentException("No selected item is available to execute");

        var runLevelCv = dto.CvVersionId;
        if (runLevelCv is { } cvId)
        {
            var ownsCv = await _db.CvVersions.AnyAsync(v => v.Id == cvId && v.Cv.UserId == userId);
            if (!ownsCv) throw new ArgumentException("CV version not found");
        }

        var skippedNoEmail = 0;
        var rows = new List<BulkApplyRowInputDto>();
        foreach (var item in usable)
        {
            if (string.IsNullOrWhiteSpace(item.RecipientEmail))
            {
                // Fail the item here rather than queueing a row the executor would skip — the
                // user gets an explicit count instead of a mysterious "skipped" row.
                item.Status = "failed";
                item.ErrorMessage = "No email address";
                item.UpdatedAt = DateTime.UtcNow;
                skippedNoEmail++;
                continue;
            }

            rows.Add(new BulkApplyRowInputDto
            {
                CompanyName = item.CompanyName,
                Location = item.Location,
                Specialty = item.PositionTitle,
                RecipientEmail = item.RecipientEmail,
                RecipientName = item.RecipientName,
                Subject = item.Subject ?? string.Empty,
                Body = item.Body ?? string.Empty,
                CvVersionId = item.CvVersionId ?? runLevelCv
            });
        }

        if (rows.Count == 0)
        {
            await _db.SaveChangesAsync();
            throw new ArgumentException("None of the selected items has a recipient email");
        }

        var createDto = new CreateBulkApplyRunDto
        {
            CvVersionId = runLevelCv,
            DelaySeconds = Math.Clamp(dto.DelaySeconds, 0, 120),
            Rows = rows
        };

        // CreateRunAsync validates ownership of every referenced CV and persists the run,
        // then the controller hands the run id to the background worker.
        var run = await _bulk.CreateRunAsync(userId, createDto);

        // Mirror: link each queued item to its run row (rows are already tracked in this
        // context from CreateRunAsync). Matching is by company+recipient since the run DTO
        // carries no item link.
        var runId = run.Id;
        var runRows = await _db.BulkApplyRows
            .Where(r => r.RunId == runId)
            .ToListAsync();
        var savedItems = await _db.ApplyQueueItems
            .Where(i => i.UserId == userId && dto.ItemIds.Contains(i.Id))
            .ToListAsync();
        var queuedCount = 0;
        foreach (var rowItem in savedItems)
        {
            if (rowItem.Status == "failed") continue; // the skippedNoEmail batch above
            var match = runRows.FirstOrDefault(r =>
                r.CompanyName == rowItem.CompanyName &&
                r.RecipientEmail == rowItem.RecipientEmail &&
                r.ApplyQueueItemId == null);
            if (match is null) continue;
            match.ApplyQueueItemId = rowItem.Id;
            rowItem.Status = "queued";
            rowItem.BulkRunId = runId;
            rowItem.UpdatedAt = DateTime.UtcNow;
            queuedCount++;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Apply queue: executed {Count} item(s) as run {RunId} (skipped {Skipped})",
            queuedCount, runId, skippedNoEmail);

        return new ExecuteApplyQueueResult
        {
            RunId = runId,
            TotalRows = run.TotalRows,
            SkippedNoEmail = skippedNoEmail
        };
    }

    private static ApplyQueueItemDto Map(ApplyQueueItem i, string? applicationStatus) => new()
    {
        Id = i.Id,
        Source = i.Source,
        ApplicationId = i.ApplicationId,
        ApplicationStatus = applicationStatus,
        ContactId = i.ContactId,
        CompanyId = i.CompanyId,
        CompanyName = i.CompanyName,
        Location = i.Location,
        PositionTitle = i.PositionTitle,
        RecipientEmail = i.RecipientEmail,
        RecipientName = i.RecipientName,
        Subject = i.Subject,
        Body = i.Body,
        CvVersionId = i.CvVersionId,
        Status = i.Status,
        BulkRunId = i.BulkRunId,
        ErrorMessage = i.ErrorMessage,
        CreatedAt = i.CreatedAt,
        SentAt = i.SentAt
    };
}
