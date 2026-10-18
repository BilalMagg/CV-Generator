using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

/// <summary>
/// Creates and drives bulk-apply runs. A run is a batch of companies/emails executed
/// sequentially by <see cref="BackgroundServices.BulkApplyBackgroundService"/> with a
/// configurable delay between sends. Each row reuses <see cref="ApplyService"/> so the
/// tracking side effects (company/contact upsert, SAVED application, SENT attempt) are
/// identical to the single-apply wizard.
/// </summary>
public class BulkApplyService
{
    public const int MaxRows = 200;

    private readonly AppDbContext _db;
    private readonly ApplyService _apply;
    private readonly ILogger<BulkApplyService> _logger;

    public BulkApplyService(AppDbContext db, ApplyService apply, ILogger<BulkApplyService> logger)
    {
        _db = db;
        _apply = apply;
        _logger = logger;
    }

    public async Task<BulkApplyRunDto> CreateRunAsync(Guid userId, CreateBulkApplyRunDto dto)
    {
        var prepared = new List<BulkApplyRow>();
        var order = 0;
        foreach (var r in dto.Rows)
        {
            if (string.IsNullOrWhiteSpace(r.CompanyName)) continue;
            prepared.Add(new BulkApplyRow
            {
                Id = Guid.NewGuid(),
                SortOrder = order++,
                CompanyName = r.CompanyName.Trim(),
                Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim(),
                Specialty = string.IsNullOrWhiteSpace(r.Specialty) ? null : r.Specialty.Trim(),
                RecipientEmail = string.IsNullOrWhiteSpace(r.RecipientEmail) ? null : r.RecipientEmail.Trim(),
                RecipientName = string.IsNullOrWhiteSpace(r.RecipientName) ? null : r.RecipientName.Trim(),
                Subject = r.Subject ?? string.Empty,
                Body = r.Body ?? string.Empty,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (prepared.Count == 0) throw new ArgumentException("Add at least one company row");
        if (prepared.Count > MaxRows) throw new ArgumentException($"Too many rows (maximum {MaxRows} per run)");

        if (dto.CvVersionId is { } cvId)
        {
            var owns = await _db.CvVersions.AnyAsync(v => v.Id == cvId && v.Cv.UserId == userId);
            if (!owns) throw new ArgumentException("CV version not found");
        }

        var run = new BulkApplyRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = "pending",
            TotalRows = prepared.Count,
            DelaySeconds = Math.Clamp(dto.DelaySeconds, 0, 120),
            CvVersionId = dto.CvVersionId,
            CreatedAt = DateTime.UtcNow
        };
        run.Rows = prepared;

        _db.BulkApplyRuns.Add(run);
        await _db.SaveChangesAsync();

        return Map(run);
    }

    public async Task<BulkApplyRunDto?> GetRunAsync(Guid userId, Guid runId)
    {
        var run = await _db.BulkApplyRuns.AsNoTracking()
            .Include(r => r.Rows)
            .FirstOrDefaultAsync(r => r.Id == runId && r.UserId == userId);
        return run is null ? null : Map(run);
    }

    /// <summary>Executed by the background worker inside its own DI scope.</summary>
    public async Task ExecuteRunAsync(Guid runId, CancellationToken ct)
    {
        var run = await _db.BulkApplyRuns.Include(r => r.Rows).FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            _logger.LogWarning("Bulk apply run {RunId} not found", runId);
            return;
        }
        if (run.Status is "completed" or "cancelled" or "failed")
            return;

        run.Status = "running";
        await _db.SaveChangesAsync(ct);

        var ordered = run.Rows.OrderBy(r => r.SortOrder).ToList();
        string? fatalError = null;
        var cancelled = false;

        try
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                var row = ordered[i];
                ct.ThrowIfCancellationRequested();
                if (row.Status != "pending") continue;

                if (string.IsNullOrWhiteSpace(row.RecipientEmail))
                {
                    row.Status = "skipped";
                    row.ErrorMessage = "No email address";
                    row.UpdatedAt = DateTime.UtcNow;
                    run.SkippedCount++;
                    run.ProcessedRows++;
                    await _db.SaveChangesAsync(ct);
                    continue;
                }

                row.Status = "sending";
                row.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                try
                {
                    var result = await _apply.ApplyAsync(run.UserId, new ApplyEmailRequest
                    {
                        CompanyName = row.CompanyName,
                        PositionTitle = string.IsNullOrWhiteSpace(row.Specialty) ? "Application" : row.Specialty!,
                        RecipientEmail = row.RecipientEmail!,
                        RecipientName = row.RecipientName,
                        Subject = row.Subject ?? string.Empty,
                        Body = row.Body ?? string.Empty,
                        CvVersionId = run.CvVersionId,
                        OfferSource = "bulk_apply",
                        AllowDuplicate = true
                    });

                    row.ApplicationId = result.ApplicationId;
                    row.ContactId = result.ContactId;
                    row.CompanyId = result.CompanyId;

                    if (result.SentNow)
                    {
                        row.Status = "sent";
                        row.SentAt = DateTime.UtcNow;
                        run.SentCount++;
                    }
                    else
                    {
                        row.Status = "failed";
                        row.ErrorMessage = Truncate(result.SendError ?? "Send failed");
                        run.FailedCount++;
                        if (result.NeedsReauth)
                        {
                            fatalError = "Gmail session expired — reconnect Gmail, then retry the failed rows.";
                            row.UpdatedAt = DateTime.UtcNow;
                            run.ProcessedRows++;
                            await _db.SaveChangesAsync(ct);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    row.Status = "failed";
                    row.ErrorMessage = Truncate(ex.Message);
                    run.FailedCount++;
                    _logger.LogWarning(ex, "Bulk apply row {RowId} failed", row.Id);
                }

                row.UpdatedAt = DateTime.UtcNow;
                run.ProcessedRows++;
                await _db.SaveChangesAsync(ct);

                if (run.DelaySeconds > 0 && i < ordered.Count - 1)
                    await Task.Delay(TimeSpan.FromSeconds(run.DelaySeconds), ct);
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            _logger.LogInformation("Bulk apply run {RunId} cancelled", runId);
        }

        foreach (var rest in ordered.Where(r => r.Status == "pending"))
        {
            rest.Status = "skipped";
            rest.ErrorMessage = cancelled
                ? "Cancelled"
                : fatalError != null ? "Skipped — Gmail reconnect required" : "Not processed";
            rest.UpdatedAt = DateTime.UtcNow;
            run.SkippedCount++;
            run.ProcessedRows++;
        }

        run.Status = cancelled ? "cancelled" : fatalError != null ? "failed" : "completed";
        run.CompletedAt = DateTime.UtcNow;
        if (cancelled) run.CancelledAt = DateTime.UtcNow;
        if (fatalError != null) run.ErrorMessage = fatalError;
        await _db.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation("Bulk apply run {RunId} finished: {Status} (sent {Sent}, failed {Failed}, skipped {Skipped})",
            runId, run.Status, run.SentCount, run.FailedCount, run.SkippedCount);
    }

    private static string Truncate(string value) => value.Length <= 2000 ? value : value[..2000];

    private static BulkApplyRunDto Map(BulkApplyRun run) => new()
    {
        Id = run.Id,
        Status = run.Status,
        TotalRows = run.TotalRows,
        ProcessedRows = run.ProcessedRows,
        SentCount = run.SentCount,
        FailedCount = run.FailedCount,
        SkippedCount = run.SkippedCount,
        DelaySeconds = run.DelaySeconds,
        CvVersionId = run.CvVersionId,
        ErrorMessage = run.ErrorMessage,
        CreatedAt = run.CreatedAt,
        CompletedAt = run.CompletedAt,
        CancelledAt = run.CancelledAt,
        Rows = run.Rows
            .OrderBy(r => r.SortOrder)
            .Select(r => new BulkApplyRowDto
            {
                Id = r.Id,
                SortOrder = r.SortOrder,
                CompanyName = r.CompanyName,
                Location = r.Location,
                Specialty = r.Specialty,
                RecipientEmail = r.RecipientEmail,
                RecipientName = r.RecipientName,
                Subject = r.Subject,
                Status = r.Status,
                ErrorMessage = r.ErrorMessage,
                ApplicationId = r.ApplicationId,
                ContactId = r.ContactId,
                CompanyId = r.CompanyId,
                SentAt = r.SentAt
            }).ToList()
    };
}
