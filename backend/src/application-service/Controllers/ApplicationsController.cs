using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApplicationService.DTOs;
using ApplicationService.Services;
using ApplicationService.Validators;
using FluentValidation;

namespace ApplicationService.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _service;
    private readonly IValidator<CreateApplicationDto> _createValidator;
    private readonly IValidator<UpdateStatusDto> _statusValidator;
    private readonly IValidator<UpdateApplicationDto> _updateValidator;
    private readonly IValidator<DuplicateCheckRequestDto> _duplicateValidator;
    private readonly IValidator<CreateAttemptDto> _createAttemptValidator;
    private readonly IValidator<UpdateAttemptDto> _updateAttemptValidator;
    private readonly ILogger<ApplicationsController> _logger;

    public ApplicationsController(
        IApplicationService service,
        IValidator<CreateApplicationDto> createValidator,
        IValidator<UpdateStatusDto> statusValidator,
        IValidator<UpdateApplicationDto> updateValidator,
        IValidator<DuplicateCheckRequestDto> duplicateValidator,
        IValidator<CreateAttemptDto> createAttemptValidator,
        IValidator<UpdateAttemptDto> updateAttemptValidator,
        ILogger<ApplicationsController> logger)
    {
        _service = service;
        _createValidator = createValidator;
        _statusValidator = statusValidator;
        _updateValidator = updateValidator;
        _duplicateValidator = duplicateValidator;
        _createAttemptValidator = createAttemptValidator;
        _updateAttemptValidator = updateAttemptValidator;
        _logger = logger;
    }

    private string? GetUserId()
    {
        return Request.Headers["X-User-Id"].FirstOrDefault()
            ?? User.FindFirst("user_id")?.Value
            ?? User.FindFirst("sub")?.Value;
    }

    private Guid? GetUserCandidateId()
    {
        var sub = GetUserId();
        if (sub == null) return null;
        return Guid.TryParse(sub, out var guid) ? guid : null;
    }

    private async Task<bool> OwnsApplicationAsync(Guid appId, Guid candidateId)
    {
        var app = await _service.GetByIdAsync(appId);
        return app?.CandidateId == candidateId;
    }

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
        [FromQuery] DateTime? updatedTo = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 20;

        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var statusArr = !string.IsNullOrWhiteSpace(statuses)
            ? statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

        var result = await _service.GetAllAsync(candidateId, page, pageSize, statusArr, search,
            appliedFrom, appliedTo, updatedFrom, updatedTo);
        return Ok(ApiResponse<ApplicationListDto>.Ok(result));
    }

    /// GET /applications/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        if (!await OwnsApplicationAsync(id, candidateId.Value))
            return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));

        var app = await _service.GetByIdAsync(id);
        if (app == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
        return Ok(ApiResponse<ApplicationResponseDto>.Ok(app));
    }

    /// POST /applications/check-duplicate
    [HttpPost("check-duplicate")]
    public async Task<IActionResult> CheckDuplicate([FromBody] DuplicateCheckRequestDto dto)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var validation = await _duplicateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<DuplicateCheckResponseDto>.Error(validation.Errors.First().ErrorMessage));

        var result = await _service.CheckDuplicatesAsync(candidateId.Value, dto);
        return Ok(ApiResponse<DuplicateCheckResponseDto>.Ok(result));
    }

    /// POST /applications
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationDto dto)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(validation.Errors.First().ErrorMessage));

        // Force CandidateId to the authenticated user
        dto = dto with { CandidateId = candidateId.Value };

        try
        {
            var created = await _service.CreateAsync(dto, GetUserId());
            return Created($"/api/applications/{created.Id}", ApiResponse<ApplicationResponseDto>.Created(created));
        }
        catch (DuplicateApplicationException ex)
        {
            return Conflict(ApiResponse<ApplicationResponseDto>.Error(ex.Message, ex.Payload));
        }
    }

    /// PATCH /applications/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        if (!await OwnsApplicationAsync(id, candidateId.Value))
            return NotFound(ApiResponse<object>.Error("Application not found"));

        var validation = await _statusValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(validation.Errors.First().ErrorMessage));

        var updated = await _service.UpdateStatusAsync(id, dto, GetUserId());
        if (updated == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
        return Ok(ApiResponse<ApplicationResponseDto>.Ok(updated));
    }

    /// PUT /applications/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApplicationDto dto)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        if (!await OwnsApplicationAsync(id, candidateId.Value))
            return NotFound(ApiResponse<object>.Error("Application not found"));

        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(validation.Errors.First().ErrorMessage));

        var updated = await _service.UpdateDetailsAsync(id, dto, GetUserId());
        if (updated == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
        return Ok(ApiResponse<ApplicationResponseDto>.Ok(updated));
    }

    /// DELETE /applications/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        if (!await OwnsApplicationAsync(id, candidateId.Value))
            return NotFound(ApiResponse<object>.Error("Application not found"));

        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Application not found"));
        return NoContent();
    }

    /// GET /applications/statistics
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var stats = await _service.GetStatisticsAsync(candidateId);
        return Ok(ApiResponse<ApplicationStatisticsDto>.Ok(stats));
    }

    /// GET /applications/statistics/trends
    [HttpGet("statistics/trends")]
    public async Task<IActionResult> GetTrends()
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var trends = await _service.GetTrendsAsync(candidateId);
        return Ok(ApiResponse<StatisticsTrendsDto>.Ok(trends));
    }

    /// PATCH /applications/{id}/toggle-save
    [HttpPatch("{id}/toggle-save")]
    public async Task<IActionResult> ToggleSave(Guid id)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        if (!await OwnsApplicationAsync(id, candidateId.Value))
            return NotFound(ApiResponse<bool>.Error("Application not found"));

        var isSaved = await _service.ToggleSaveAsync(id, GetUserId());
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
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var statusArr = !string.IsNullOrWhiteSpace(statuses)
            ? statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

        var events = await _service.GetCalendarEventsAsync(candidateId.Value, from, to, statusArr);
        return Ok(ApiResponse<List<CalendarEventDto>>.Ok(events));
    }

    /// GET /applications/activity
    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] int limit = 50)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var feed = await _service.GetActivityFeedAsync(candidateId, limit);
        return Ok(ApiResponse<ActivityFeedDto>.Ok(feed));
    }

    /// GET /applications/{id}/attempts
    [HttpGet("{id}/attempts")]
    public async Task<IActionResult> GetAttempts(Guid id)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        if (!await OwnsApplicationAsync(id, candidateId.Value))
            return NotFound(ApiResponse<List<AttemptResponseDto>>.Error("Application not found"));

        var attempts = await _service.GetAttemptsAsync(id);
        return Ok(ApiResponse<List<AttemptResponseDto>>.Ok(attempts));
    }

    /// GET /applications/{id}/attempts/{attemptId}
    [HttpGet("{id}/attempts/{attemptId}")]
    public async Task<IActionResult> GetAttempt(Guid id, Guid attemptId)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<AttemptResponseDto>.Error("Unable to determine user identity"));

        if (!await OwnsApplicationAsync(id, candidateId.Value))
            return NotFound(ApiResponse<AttemptResponseDto>.Error("Application not found"));

        var attempt = await _service.GetAttemptAsync(id, attemptId);
        if (attempt == null) return NotFound(ApiResponse<AttemptResponseDto>.Error("Attempt not found"));
        return Ok(ApiResponse<AttemptResponseDto>.Ok(attempt));
    }

    /// POST /applications/{id}/attempts
    [HttpPost("{id}/attempts")]
    public async Task<IActionResult> CreateAttempt(Guid id, [FromBody] CreateAttemptDto dto)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<AttemptResponseDto>.Error("Unable to determine user identity"));

        if (!await OwnsApplicationAsync(id, candidateId.Value))
            return NotFound(ApiResponse<AttemptResponseDto>.Error("Application not found"));

        var validation = await _createAttemptValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<AttemptResponseDto>.Error(validation.Errors.First().ErrorMessage));

        try
        {
            var created = await _service.CreateAttemptAsync(id, dto, GetUserId());
            return Created($"/api/applications/{id}/attempts/{created.Id}", ApiResponse<AttemptResponseDto>.Created(created));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<AttemptResponseDto>.Error("Application not found"));
        }
    }

    /// PATCH /applications/{id}/attempts/{attemptId}
    [HttpPatch("{id}/attempts/{attemptId}")]
    public async Task<IActionResult> UpdateAttempt(Guid id, Guid attemptId, [FromBody] UpdateAttemptDto dto)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<AttemptResponseDto>.Error("Unable to determine user identity"));

        if (!await OwnsApplicationAsync(id, candidateId.Value))
            return NotFound(ApiResponse<AttemptResponseDto>.Error("Application not found"));

        var validation = await _updateAttemptValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<AttemptResponseDto>.Error(validation.Errors.First().ErrorMessage));

        var updated = await _service.UpdateAttemptAsync(id, attemptId, dto);
        if (updated == null) return NotFound(ApiResponse<AttemptResponseDto>.Error("Attempt not found"));
        return Ok(ApiResponse<AttemptResponseDto>.Ok(updated));
    }

    /// POST /applications/seed
    [HttpPost("seed")]
    public async Task<IActionResult> Seed([FromBody] SeedApplicationsDto dto)
    {
        var candidateId = GetUserCandidateId();
        if (candidateId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var seeder = HttpContext.RequestServices.GetRequiredService<IDataSeeder>();
        var result = await seeder.GenerateApplicationsAsync(candidateId.Value, dto.Count, dto.MonthsBack);
        return Ok(ApiResponse<SeedResultDto>.Ok(result));
    }
}
