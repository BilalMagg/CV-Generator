using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CV_Generator.Dto;
using CV_Generator.Services;
using CV_Generator.Services.BackgroundServices;

namespace CV_Generator.Controllers;

/// <summary>
/// The Apply Queue: staging container for applies composed in the bulk-apply or quick-apply
/// wizards that the user parked instead of sending. Executes a selection as a real bulk-apply
/// run (sequential sends with pacing, live per-row progress on the bulk-apply page).
/// </summary>
[ApiController]
[Authorize]
[Route("api/applications/queue")]
public class ApplyQueueController : ControllerBase
{
    private readonly ApplyQueueService _queue;
    private readonly ICurrentUserService _currentUser;
    private readonly IGmailAuthService _gmailAuth;
    private readonly ILogger<ApplyQueueController> _logger;

    public ApplyQueueController(
        ApplyQueueService queue,
        ICurrentUserService currentUser,
        IGmailAuthService gmailAuth,
        ILogger<ApplyQueueController> logger)
    {
        _queue = queue;
        _currentUser = currentUser;
        _gmailAuth = gmailAuth;
        _logger = logger;
    }

    /// <summary>Lists queue items, newest first. Default filter: "pending".</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status = "pending")
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var items = await _queue.ListAsync(userId.Value, status);
        return Ok(ApiResponse<List<ApplyQueueItemDto>>.Ok(items));
    }

    /// <summary>Parks a composed apply in the queue (creates the SAVED application immediately).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplyQueueItemDto dto)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        try
        {
            var item = await _queue.CreateItemAsync(userId.Value, dto);
            return Ok(ApiResponse<ApplyQueueItemDto>.Ok(item));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Parks several applies at once (the bulk-apply wizard saves its whole row set).</summary>
    [HttpPost("batch")]
    public async Task<IActionResult> CreateBatch([FromBody] List<CreateApplyQueueItemDto> dtos)
    {
        if (dtos.Count == 0) return BadRequest(ApiResponse<object>.Error("No applies to stage"));
        if (dtos.Count > BulkApplyService.MaxRows)
            return BadRequest(ApiResponse<object>.Error($"Too many applies (maximum {BulkApplyService.MaxRows})"));

        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        try
        {
            var items = await _queue.CreateItemsAsync(userId.Value, dtos);
            return Ok(ApiResponse<List<ApplyQueueItemDto>>.Ok(items));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var ok = await _queue.DeleteAsync(userId.Value, id);
        if (!ok) return NotFound(ApiResponse<object>.Error("Queue item not found"));
        return Ok(ApiResponse<object>.Ok(new { deleted = true }));
    }

    /// <summary>
    /// Executes a selection of pending items as a bulk-apply run and starts it. Gmail must be
    /// usable before the run is created — same fail-fast guard as the normal bulk-apply create.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] ExecuteApplyQueueDto dto,
        [FromServices] BulkApplyBackgroundService backgroundService)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var gmail = await _gmailAuth.GetStatusAsync(userId.Value);
        if (gmail is null)
        {
            return BadRequest(ApiResponse<object>.Error(
                "Gmail is not connected. Use \"Connect to Gmail\" below, then execute again."));
        }
        if (gmail.NeedsReauth)
        {
            return BadRequest(ApiResponse<object>.Error(
                "Gmail session expired — use \"Connect to Gmail\" below to re-authorize, then execute again."));
        }

        try
        {
            var result = await _queue.ExecuteAsync(userId.Value, dto);
            await backgroundService.EnqueueRunAsync(result.RunId);
            return Accepted(ApiResponse<ExecuteApplyQueueResult>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message));
        }
    }
}
