using System.Text.Json;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService;
using WorkflowService.Entity;
using WorkflowService.Models;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(WorkflowDbContext db, ILogger<WorkflowsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var workflows = await _db.Workflows.ToListAsync();
        return Ok(ApiResponse<List<Workflow>>.Ok(workflows));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var workflow = await _db.Workflows.FindAsync(id);
        if (workflow == null) return NotFound(ApiResponse<Workflow>.Error("Workflow not found"));
        return Ok(ApiResponse<Workflow>.Ok(workflow));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDto dto)
    {
        var workflow = new Workflow
        {
            Name = dto.Name,
            Description = dto.Description,
            DefinitionJson = dto.DefinitionJson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created workflow {Id}", workflow.Id);
        return Created($"/api/workflows/{workflow.Id}", ApiResponse<Workflow>.Created(workflow));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowDto dto)
    {
        var workflow = await _db.Workflows.FindAsync(id);
        if (workflow == null) return NotFound(ApiResponse<Workflow>.Error("Workflow not found"));

        workflow.Name = dto.Name;
        workflow.Description = dto.Description;
        workflow.DefinitionJson = dto.DefinitionJson;
        workflow.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Workflow>.Ok(workflow));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var workflow = await _db.Workflows.FindAsync(id);
        if (workflow == null) return NotFound(ApiResponse<object>.Error("Workflow not found"));

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("generate-cv")]
    public async Task<IActionResult> GenerateTailoredCv(
        [FromBody] GenerateCvRequest request,
        [FromServices] Services.CvGenerationBackgroundService backgroundService)
    {
        if (request == null || request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.JobDescription))
            return BadRequest(ApiResponse<object>.Error("Invalid request payload. UserId and JobDescription are required."));

        var run = new CvGenerationRun
        {
            UserId = request.UserId,
            JobDescription = request.JobDescription,
            CandidateName = request.CandidateName,
            RecipientEmail = request.RecipientEmail,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.CvGenerationRuns.Add(run);
        await _db.SaveChangesAsync();

        await backgroundService.EnqueueRunAsync(run.Id);

        _logger.LogInformation("Enqueued CV generation run {RunId} for User {UserId}", run.Id, request.UserId);
        return Accepted(ApiResponse<CvGenerationSubmitResponse>.Ok(new CvGenerationSubmitResponse { RunId = run.Id }));
    }

    [HttpGet("generate-cv/{runId:guid}/status")]
    public async Task<IActionResult> GetRunStatus(Guid runId)
    {
        var run = await _db.CvGenerationRuns.FindAsync(runId);
        if (run == null) return NotFound(ApiResponse<object>.Error("Run not found"));

        var steps = DeserializeSteps(run.StepStatuses);

        var response = new CvGenerationStatusResponse
        {
            RunId = run.Id,
            Status = run.Status,
            CurrentStep = run.CurrentStep,
            Steps = steps,
            ErrorMessage = run.ErrorMessage,
            CreatedAt = run.CreatedAt,
            CompletedAt = run.CompletedAt,
            CancelledAt = run.CancelledAt
        };

        return Ok(ApiResponse<CvGenerationStatusResponse>.Ok(response));
    }

    [HttpGet("generate-cv/{runId:guid}/result")]
    public async Task<IActionResult> GetRunResult(Guid runId)
    {
        var run = await _db.CvGenerationRuns.FindAsync(runId);
        if (run == null) return NotFound(ApiResponse<object>.Error("Run not found"));

        if (run.Status != "completed")
            return BadRequest(ApiResponse<object>.Error("Run has not completed yet. Current status: " + run.Status));

        var response = new CvGenerationResultResponse
        {
            RunId = run.Id,
            Extraction = DeserializeJson(run.ExtractionResult),
            Search = DeserializeJson(run.SearchResult),
            Optimization = DeserializeJson(run.OptimizationResult),
            Render = DeserializeJson(run.RenderResult),
            Delivery = DeserializeJson(run.DeliveryResult)
        };

        return Ok(ApiResponse<CvGenerationResultResponse>.Ok(response));
    }

    [HttpPost("generate-cv/{runId:guid}/cancel")]
    public async Task<IActionResult> CancelRun(
        Guid runId,
        [FromServices] Services.CvGenerationBackgroundService backgroundService)
    {
        var run = await _db.CvGenerationRuns.FindAsync(runId);
        if (run == null) return NotFound(ApiResponse<object>.Error("Run not found"));

        if (run.Status is "completed" or "cancelled" or "failed")
            return BadRequest(ApiResponse<object>.Error("Run is already in terminal state: " + run.Status));

        backgroundService.CancelRun(runId);

        _logger.LogInformation("Cancellation requested for run {RunId}", runId);
        return Ok(ApiResponse<object>.Ok(new { message = "Cancellation requested" }));
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

    public record CreateWorkflowDto(string? Name, string? Description, string? DefinitionJson);
    public record UpdateWorkflowDto(string? Name, string? Description, string? DefinitionJson, bool IsActive);
}
