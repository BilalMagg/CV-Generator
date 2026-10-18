using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CV_Generator.Dto;
using CV_Generator.Services;
using CV_Generator.Services.AgentClients;
using CV_Generator.Services.BackgroundServices;
using CV_Generator.Services.BulkExtract;

namespace CV_Generator.Controllers;

[ApiController]
[Authorize]
[Route("api/applications/bulk-apply")]
public class BulkApplyController : ControllerBase
{
    private readonly BulkApplyService _bulk;
    private readonly ICurrentUserService _currentUser;
    private readonly IBulkExtractClient _extract;
    private readonly IAgentLlmSettingsService _agentLlm;
    private readonly ILogger<BulkApplyController> _logger;

    public BulkApplyController(
        BulkApplyService bulk,
        ICurrentUserService currentUser,
        IBulkExtractClient extract,
        IAgentLlmSettingsService agentLlm,
        ILogger<BulkApplyController> logger)
    {
        _bulk = bulk;
        _currentUser = currentUser;
        _extract = extract;
        _agentLlm = agentLlm;
        _logger = logger;
    }

    /// <summary>
    /// Parse a pasted list of companies/emails into editable rows. Deterministic parsing is
    /// tried first; the LLM agent is only used as a fallback when it yields nothing.
    /// </summary>
    [HttpPost("extract")]
    public async Task<IActionResult> Extract([FromBody] BulkExtractRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(ApiResponse<object>.Error("Paste the companies list first"));

        var deterministic = BulkTextParser.Parse(request.Text);
        if (deterministic.Count > 0)
        {
            return Ok(ApiResponse<BulkExtractResultDto>.Ok(new BulkExtractResultDto
            {
                Method = "deterministic",
                Rows = deterministic
            }));
        }

        var userId = _currentUser.UserId;
        if (userId is not null)
        {
            var llm = await _agentLlm.GetProviderModelAsync(userId.Value, "bulk_extract");
            request.Provider ??= string.IsNullOrWhiteSpace(llm.Provider) ? null : llm.Provider;
            request.Model ??= string.IsNullOrWhiteSpace(llm.Model) ? null : llm.Model;
        }

        var rows = await _extract.ExtractAsync(request);
        if (rows is null)
            return StatusCode(502, ApiResponse<object>.Error("Could not parse the list — try again or fill the rows manually"));

        return Ok(ApiResponse<BulkExtractResultDto>.Ok(new BulkExtractResultDto
        {
            Method = "llm",
            Rows = rows
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBulkApplyRunDto request,
        [FromServices] BulkApplyBackgroundService backgroundService)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        try
        {
            var run = await _bulk.CreateRunAsync(userId.Value, request);
            await backgroundService.EnqueueRunAsync(run.Id);
            return Accepted(ApiResponse<BulkApplySubmitResponse>.Ok(new BulkApplySubmitResponse
            {
                RunId = run.Id,
                TotalRows = run.TotalRows
            }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
    }

    [HttpGet("{runId:guid}")]
    public async Task<IActionResult> GetRun(Guid runId)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var run = await _bulk.GetRunAsync(userId.Value, runId);
        if (run is null) return NotFound(ApiResponse<object>.Error("Run not found"));

        return Ok(ApiResponse<BulkApplyRunDto>.Ok(run));
    }

    [HttpPost("{runId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid runId,
        [FromServices] BulkApplyBackgroundService backgroundService)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var run = await _bulk.GetRunAsync(userId.Value, runId);
        if (run is null) return NotFound(ApiResponse<object>.Error("Run not found"));
        if (run.Status is "completed" or "cancelled" or "failed")
            return BadRequest(ApiResponse<object>.Error("Run is already finished: " + run.Status));

        backgroundService.CancelRun(runId);
        return Ok(ApiResponse<object>.Ok(new { message = "Cancellation requested" }));
    }
}
