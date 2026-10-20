using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

/// <summary>
/// Creates and drives bulk-apply runs. A run is a batch of companies/emails executed
/// sequentially by <see cref="BackgroundServices.BulkApplyBackgroundService"/> with a
/// configurable delay between sends. Each row is sent through <see cref="ApplyService.SendBulkRowAsync"/>
/// (a simple Mailbox-style send + best-effort tracking), so the send is the only step that
/// can fail a row and tracking can never wedge the batch.
/// </summary>
public class BulkApplyService
{
    public const int MaxRows = 200;

    /// <summary>Hard ceiling for one row's send pipeline, so a wedged network call can't stall the batch.</summary>
    private static readonly TimeSpan RowTimeout = TimeSpan.FromSeconds(180);

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
                CvVersionId = r.CvVersionId,
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

        // Validate every distinct per-row CV so a bad id fails the whole run up front rather
        // than silently sending with the wrong (or no) attachment.
        var rowCvIds = prepared.Where(r => r.CvVersionId is not null)
            .Select(r => r.CvVersionId!.Value)
            .Distinct()
            .ToList();
        if (rowCvIds.Count > 0)
        {
            var ownedIds = await _db.CvVersions
                .Where(v => rowCvIds.Contains(v.Id) && v.Cv.UserId == userId)
                .Select(v => v.Id)
                .ToListAsync();
            if (ownedIds.Count != rowCvIds.Count)
                throw new ArgumentException("One or more row CV versions were not found");
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

    /// <summary>
    /// Persists a cancel request. Combined with the in-memory token (which the background
    /// worker trips), this guarantees cancellation even when the run has not started yet,
    /// is queued behind another run, or its worker was lost to a restart.
    /// </summary>
    public async Task<bool> RequestCancelAsync(Guid userId, Guid runId)
    {
        var run = await _db.BulkApplyRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.UserId == userId);
        if (run is null) return false;
        if (run.Status is "completed" or "cancelled" or "failed") return false;

        run.CancelRequested = true;

        // A run that has not started has no live token; finalise it right here so the UI
        // sees a terminal state immediately instead of waiting for a worker that may never come.
        if (run.Status == "pending")
        {
            await FinalizeAsync(run, "cancelled", null, CancellationToken.None);
        }
        else
        {
            await _db.SaveChangesAsync();
        }
        return true;
    }

    /// <summary>
    /// Resets the run's failed rows back to "pending", recomputes the counters and clears the
    /// terminal status so the background worker can send them again. Rows that were already sent
    /// or skipped are left untouched. Returns the reloaded run, or null when it does not exist
    /// or has nothing to retry.
    /// </summary>
    public async Task<BulkApplyRunDto?> RetryFailedRowsAsync(Guid userId, Guid runId)
    {
        var run = await _db.BulkApplyRuns
            .Include(r => r.Rows)
            .FirstOrDefaultAsync(r => r.Id == runId && r.UserId == userId);
        if (run is null) return null;

        // Only a terminal run makes sense to retry; a run still in flight owns its rows.
        if (run.Status is "running" or "pending") return null;

        var retryable = run.Rows.Where(r => r.Status == "failed").ToList();
        if (retryable.Count == 0) return null;

        foreach (var row in retryable)
        {
            row.Status = "pending";
            row.ErrorMessage = null;
            row.SentAt = null;
            row.UpdatedAt = DateTime.UtcNow;
        }

        run.Status = "pending";
        run.CancelRequested = false;
        run.CompletedAt = null;
        run.CancelledAt = null;
        run.ErrorMessage = null;
        run.FailedCount = 0;
        run.ProcessedRows = run.Rows.Count(r => r.Status is "sent" or "skipped");

        await _db.SaveChangesAsync();
        _logger.LogInformation("Bulk apply run {RunId}: queued {Count} failed row(s) for retry", runId, retryable.Count);

        return Map(run);
    }

    /// <summary>
    /// Marks runs left in "running"/"pending" by a crash or restart as failed. Called once at
    /// startup — without this an interrupted run stays "running" forever and cannot be cancelled.
    /// </summary>
    public async Task<int> RecoverOrphanedRunsAsync(CancellationToken ct = default)
    {
        var orphans = await _db.BulkApplyRuns
            .Include(r => r.Rows)
            .Where(r => r.Status == "running" || r.Status == "pending")
            .ToListAsync(ct);

        foreach (var run in orphans)
        {
            // A run whose cancel was requested but never acted on should finish as cancelled.
            var status = run.CancelRequested ? "cancelled" : "failed";
            var error = run.CancelRequested
                ? null
                : "Interrupted (server restarted before the run finished).";
            await FinalizeAsync(run, status, error, CancellationToken.None);
        }

        if (orphans.Count > 0)
            _logger.LogWarning("Recovered {Count} orphaned bulk apply run(s) at startup", orphans.Count);

        return orphans.Count;
    }

    /// <summary>
    /// Public terminal-finalisation by run id, callable from the background worker as a safety
    /// net. Loads the run, finalises it (skipping pending rows, converting leftover "sending"
    /// rows to failed) and persists. No-ops when the run is already terminal.
    /// </summary>
    public async Task FinalizeRunAsync(Guid runId, string status, string? errorMessage)
    {
        var run = await _db.BulkApplyRuns.Include(r => r.Rows).FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null) return;
        if (run.Status is "completed" or "cancelled" or "failed") return;
        await FinalizeAsync(run, status, errorMessage, CancellationToken.None);
    }

    /// <summary>
    /// Watchdog entry point: finalises any run still "running"/"pending" in the DB that is NOT
    /// in <paramref name="activeRunIds"/> (i.e. its worker is gone). Returns the number fixed.
    /// </summary>
    public async Task<int> SweepOrphanedRunsAsync(HashSet<Guid> activeRunIds, CancellationToken ct = default)
    {
        var orphans = await _db.BulkApplyRuns
            .Include(r => r.Rows)
            .Where(r => (r.Status == "running" || r.Status == "pending") && !activeRunIds.Contains(r.Id))
            .ToListAsync(ct);

        foreach (var run in orphans)
        {
            var status = run.CancelRequested ? "cancelled" : "failed";
            var error = run.CancelRequested
                ? null
                : "Interrupted (the run stopped unexpectedly).";
            await FinalizeAsync(run, status, error, CancellationToken.None);
        }

        if (orphans.Count > 0)
            _logger.LogWarning("Bulk apply watchdog finalised {Count} orphaned run(s)", orphans.Count);

        return orphans.Count;
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

        // Cancellation is a persisted flag as well as an in-memory token, so a cancel that
        // arrives before the worker starts (or after a restart) is still honoured.
        if (run.CancelRequested)
        {
            await FinalizeAsync(run, "cancelled", null, CancellationToken.None);
            return;
        }

        run.Status = "running";
        await _db.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation("Bulk apply run {RunId}: executing {Count} row(s)", runId, run.Rows.Count);

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

                _logger.LogInformation("Bulk apply run {RunId}: row {Idx}/{Total} → {Company} <{Email}>",
                    runId, i + 1, ordered.Count, row.CompanyName, row.RecipientEmail);

                if (string.IsNullOrWhiteSpace(row.RecipientEmail))
                {
                    row.Status = "skipped";
                    row.ErrorMessage = "No email address";
                    row.UpdatedAt = DateTime.UtcNow;
                    run.SkippedCount++;
                    run.ProcessedRows++;
                    await SaveProgressAsync();
                    continue;
                }

                row.Status = "sending";
                row.UpdatedAt = DateTime.UtcNow;
                await SaveProgressAsync();

                try
                {
                    // Hard ceiling on a single row. Every network step inside ApplyAsync (Gmail
                    // send, CV download, token refresh) should return long before this; if any of
                    // them wedges without a timeout of its own we abandon the row here so the
                    // batch keeps moving instead of hanging on row N forever. The orphaned task
                    // is left to finish in the background (best effort).
                    var applyTask = _apply.SendBulkRowAsync(run.UserId, new BulkRowSendRequest
                    {
                        CompanyName = row.CompanyName,
                        PositionTitle = string.IsNullOrWhiteSpace(row.Specialty) ? "Application" : row.Specialty!,
                        RecipientEmail = row.RecipientEmail!,
                        RecipientName = row.RecipientName,
                        Subject = row.Subject ?? string.Empty,
                        Body = row.Body ?? string.Empty,
                        CvVersionId = row.CvVersionId ?? run.CvVersionId,
                        OfferSource = "bulk_apply"
                    });

                    var winner = await Task.WhenAny(applyTask, Task.Delay(RowTimeout, ct));
                    if (winner != applyTask)
                    {
                        ct.ThrowIfCancellationRequested();
                        ObserveSafely(applyTask);
                        throw new TimeoutException(
                            $"No response after {RowTimeout.TotalSeconds:0}s — the send was abandoned. Check the recipient address and your Gmail connection, then retry this row.");
                    }

                    var result = await applyTask;

                    row.ApplicationId = result.ApplicationId == Guid.Empty ? null : result.ApplicationId;
                    row.ContactId = result.ContactId == Guid.Empty ? null : result.ContactId;
                    row.CompanyId = result.CompanyId == Guid.Empty ? null : result.CompanyId;

                    // SendBulkRowAsync throws on any send failure, so reaching here means the
                    // email was accepted by Gmail — record the row as sent.
                    row.Status = "sent";
                    row.SentAt = DateTime.UtcNow;
                    run.SentCount++;
                }
                catch (GmailReauthRequiredException ex)
                {
                    // The stored Gmail token is dead — every remaining send would fail the
                    // same way, so stop the whole batch (leaving later rows pending) and
                    // surface a reconnect message.
                    row.Status = "failed";
                    row.ErrorMessage = Truncate(ex.Message);
                    run.FailedCount++;
                    fatalError = "Gmail session expired — reconnect Gmail, then retry the failed rows.";
                    row.UpdatedAt = DateTime.UtcNow;
                    run.ProcessedRows++;
                    await SaveProgressAsync();
                    _logger.LogWarning(ex, "Bulk apply run {RunId} stopped: Gmail re-auth required", runId);
                    break;
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
                await SaveProgressAsync();

                if (run.DelaySeconds > 0 && i < ordered.Count - 1)
                    await Task.Delay(TimeSpan.FromSeconds(run.DelaySeconds), ct);
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            _logger.LogInformation("Bulk apply run {RunId} cancelled", runId);
        }
        catch (Exception ex)
        {
            // A failure in bookkeeping (e.g. a DB blip) must NOT leave the run stuck on
            // "running" with rows stuck on "sending" — record it and let the finally below
            // finalise the run so the UI can move on.
            fatalError = Truncate($"Run aborted: {ex.Message}");
            _logger.LogError(ex, "Bulk apply run {RunId} aborted", runId);
        }

        // A cancel may also have been requested while the row was in flight.
        if (!cancelled && run.CancelRequested) cancelled = true;

        await FinalizeAsync(run, cancelled ? "cancelled" : fatalError != null ? "failed" : "completed", fatalError, CancellationToken.None);

        _logger.LogInformation("Bulk apply run {RunId} finished: {Status} (sent {Sent}, failed {Failed}, skipped {Skipped})",
            runId, run.Status, run.SentCount, run.FailedCount, run.SkippedCount);

        // Local helper: persist row/run progress; a failure here is retried once, and if it
        // still fails the outer catch converts it into a terminal failure.
        async Task SaveProgressAsync()
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bulk apply run {RunId}: progress save failed, retrying once", runId);
                await _db.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Marks any rows still pending as skipped and writes the terminal status. Idempotent and
    /// callable with an already-cancelled token — it must always succeed so a run never stays
    /// stuck in "running".
    /// </summary>
    private async Task FinalizeAsync(BulkApplyRun run, string status, string? errorMessage, CancellationToken ct)
    {
        foreach (var rest in run.Rows.Where(r => r.Status == "pending"))
        {
            rest.Status = "skipped";
            rest.ErrorMessage = status == "cancelled"
                ? "Cancelled"
                : errorMessage != null ? "Skipped — run aborted" : "Not processed";
            rest.UpdatedAt = DateTime.UtcNow;
            run.SkippedCount++;
            run.ProcessedRows++;
        }

        // Any row left mid-flight (crashed/timed-out send) is recorded as failed so it is
        // never permanently wedged in "sending".
        foreach (var stuck in run.Rows.Where(r => r.Status == "sending"))
        {
            stuck.Status = "failed";
            stuck.ErrorMessage ??= errorMessage ?? "Interrupted before completion";
            stuck.UpdatedAt = DateTime.UtcNow;
            run.FailedCount++;
        }

        run.Status = status;
        run.CompletedAt = DateTime.UtcNow;
        if (status == "cancelled") run.CancelledAt = DateTime.UtcNow;
        if (errorMessage != null) run.ErrorMessage = errorMessage;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk apply run {RunId}: could not persist final status {Status}", run.Id, status);
        }
    }

    private static string Truncate(string value) => value.Length <= 2000 ? value : value[..2000];

    /// <summary>Swallows the result/exception of an abandoned task so it never surfaces as unobserved.</summary>
    private void ObserveSafely(Task task)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception is { } ex)
                _logger.LogWarning(ex, "Abandoned bulk-apply row task ended with an error after timeout");
        }, TaskScheduler.Default);
    }

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
        CancelRequested = run.CancelRequested,
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
                CvVersionId = r.CvVersionId ?? run.CvVersionId,
                SentAt = r.SentAt
            }).ToList()
    };
}
