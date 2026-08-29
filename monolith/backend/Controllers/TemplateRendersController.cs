using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;
using CV_Generator.Services.BackgroundServices;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/workflows/template-renders")]
public class TemplateRendersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<TemplateRendersController> _logger;

    public TemplateRendersController(AppDbContext db, ILogger<TemplateRendersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] TemplateRenderRequest request,
        [FromServices] TemplateRenderBackgroundService backgroundService)
    {
        if (request.UserId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Error("UserId is required"));
        if (request.ExtractionId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Error("extraction_id is required — run Job Extraction first"));

        var run = new TemplateRenderRun
        {
            UserId = request.UserId,
            ExtractionId = request.ExtractionId,
            TemplateId = request.TemplateId,
            Language = request.Language,
            Tone = request.Tone,
            SaveToDocuments = request.SaveToDocuments,
            Title = request.Title,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.TemplateRenderRuns.Add(run);
        await _db.SaveChangesAsync();

        await backgroundService.EnqueueRunAsync(run.Id);

        _logger.LogInformation("Enqueued template render run {RunId} for User {UserId} (extraction {ExtractionId}, template '{TemplateId}')",
            run.Id, request.UserId, request.ExtractionId, request.TemplateId);
        return Accepted(ApiResponse<TemplateRenderSubmitResponse>.Ok(new TemplateRenderSubmitResponse { RunId = run.Id }));
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var run = await _db.TemplateRenderRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (run == null) return NotFound(ApiResponse<object>.Error("Run not found"));

        return Ok(ApiResponse<TemplateRenderStatusResponse>.Ok(new TemplateRenderStatusResponse
        {
            RunId = run.Id,
            Status = run.Status,
            CurrentStep = run.CurrentStep,
            Steps = DeserializeSteps(run.StepStatuses),
            ErrorMessage = run.ErrorMessage,
            CreatedAt = run.CreatedAt,
            CompletedAt = run.CompletedAt,
            CancelledAt = run.CancelledAt
        }));
    }

    [HttpGet("{id:guid}/result")]
    public async Task<IActionResult> GetResult(Guid id)
    {
        var run = await _db.TemplateRenderRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (run == null) return NotFound(ApiResponse<object>.Error("Run not found"));

        if (run.Status is "pending" or "running")
            return BadRequest(ApiResponse<object>.Error("Run is still in progress. Current status: " + run.Status));

        return Ok(ApiResponse<TemplateRenderResultResponse>.Ok(ToResult(run)));
    }

    [HttpGet("{id:guid}/events")]
    public async Task StreamEvents(Guid id, CancellationToken ct)
    {
        var response = Response;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        Response.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await using var writer = new StreamWriter(response.Body, new UTF8Encoding(false)) { AutoFlush = false };

        var alive = await _db.TemplateRenderRuns.AsNoTracking().AnyAsync(r => r.Id == id, ct);
        if (!alive)
        {
            await WriteEventAsync(writer, new TemplateRenderSseEvent
            {
                Type = "error",
                ErrorMessage = "Run not found"
            }, ct);
            return;
        }

        string? lastSnapshot = null;
        var iterations = 0L;

        while (!ct.IsCancellationRequested)
        {
            var run = await _db.TemplateRenderRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
            if (run == null) break;

            var snapshot = JsonSerializer.Serialize(new
            {
                run.Status,
                run.CurrentStep,
                run.ErrorMessage,
                Steps = DeserializeSteps(run.StepStatuses),
                run.CreatedAt,
                run.CompletedAt,
                run.CancelledAt
            });

            // Push every change (plus an initial snapshot as soon as it exists).
            if (snapshot != lastSnapshot)
            {
                lastSnapshot = snapshot;
                await WriteEventAsync(writer, new TemplateRenderSseEvent
                {
                    Type = "snapshot",
                    Status = run.Status,
                    CurrentStep = run.CurrentStep,
                    Steps = DeserializeSteps(run.StepStatuses),
                    ErrorMessage = run.ErrorMessage
                }, ct);
            }

            if (run.Status is "completed" or "failed" or "cancelled")
            {
                var done = new TemplateRenderSseEvent
                {
                    Type = "done",
                    Status = run.Status,
                    Steps = DeserializeSteps(run.StepStatuses),
                    ErrorMessage = run.ErrorMessage
                };
                if (run.Status == "completed")
                {
                    done.CurrentStep = run.CurrentStep;
                    done.Result = ToResult(run);
                }
                await WriteEventAsync(writer, done, ct);
                _logger.LogInformation("Run {RunId}: SSE stream closed ({Status})", id, run.Status);
                break;
            }

            // Heartbeat so proxies keep the stream alive during long agent calls.
            if (++iterations % 20 == 0)
            {
                await writer.WriteLineAsync(": ping");
                await writer.FlushAsync(ct);
            }

            await Task.Delay(500, ct);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromServices] TemplateRenderBackgroundService backgroundService)
    {
        var run = await _db.TemplateRenderRuns.FindAsync(id);
        if (run == null) return NotFound(ApiResponse<object>.Error("Run not found"));

        if (run.Status is "completed" or "cancelled" or "failed")
            return BadRequest(ApiResponse<object>.Error("Run is already in terminal state: " + run.Status));

        backgroundService.CancelRun(id);

        _logger.LogInformation("Cancellation requested for template render run {RunId}", id);
        return Ok(ApiResponse<object>.Ok(new { message = "Cancellation requested" }));
    }

    private static async Task WriteEventAsync(StreamWriter writer, TemplateRenderSseEvent payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        await writer.WriteAsync("data: ".AsMemory(), ct);
        await writer.WriteLineAsync(json.AsMemory(), ct);
        await writer.WriteLineAsync(string.Empty.AsMemory(), ct);
        await writer.FlushAsync(ct);
    }

    private static TemplateRenderResultResponse ToResult(TemplateRenderRun run)
    {
        SearchOutput? search = null;
        if (!string.IsNullOrWhiteSpace(run.SearchResult))
        {
            try { search = JsonSerializer.Deserialize<SearchOutput>(run.SearchResult); }
            catch { /* keep null */ }
        }

        var tex = ExtractTex(run.RenderResult);

        return new TemplateRenderResultResponse
        {
            RunId = run.Id,
            Extraction = DeserializeJson(run.ExtractionJson),
            Search = DeserializeJson(run.SearchResult),
            Render = DeserializeJson(run.RenderResult),
            Tex = tex,
            PdfUrl = run.PdfUrl,
            CvId = run.CvId,
            CvVersionId = run.CvVersionId,
            Saved = run.CvId != null,
            MatchScore = search?.MatchScore,
            GapSkills = search?.GapSkills ?? new List<string>()
        };
    }

    private static string ExtractTex(string? renderJson)
    {
        if (string.IsNullOrWhiteSpace(renderJson)) return "";
        try
        {
            using var doc = JsonDocument.Parse(renderJson);
            if (doc.RootElement.TryGetProperty("cv_code", out var code))
                return code.GetString() ?? "";
        }
        catch
        {
            // fall through
        }
        return "";
    }

    private static List<StepStatusDto> DeserializeSteps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<StepStatusDto>();
        try { return JsonSerializer.Deserialize<List<StepStatusDto>>(json) ?? new(); }
        catch { return new List<StepStatusDto>(); }
    }

    private static object? DeserializeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<object>(json); }
        catch { return json; }
    }
}