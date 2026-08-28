using Microsoft.AspNetCore.Mvc;
using CV_Generator;
using CV_Generator.Dto;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[Route("api/email-schedules")]
public class EmailSchedulesController : BaseApiController
{
    private readonly EmailScheduleService _scheduleSvc;

    public EmailSchedulesController(ICurrentUserService currentUser, EmailScheduleService scheduleSvc)
        : base(currentUser)
    {
        _scheduleSvc = scheduleSvc;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        var result = await _scheduleSvc.GetSchedulesAsync(userId);
        return Ok(ApiResponse<List<EmailScheduleDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetUserId();
        var result = await _scheduleSvc.GetScheduleAsync(id, userId);
        if (result is null) return NotFound(ApiResponse<EmailScheduleDto>.Error("Schedule not found"));
        return Ok(ApiResponse<EmailScheduleDto>.Ok(result));
    }

    /// <summary>Fires the schedule immediately through the normal pipeline.</summary>
    [HttpPost("{id}/run-now")]
    public async Task<IActionResult> RunNow(Guid id)
    {
        var userId = GetUserId();
        var result = await _scheduleSvc.RunNowAsync(id, userId);
        if (result is null) return NotFound(ApiResponse<object>.Error("Schedule not found"));
        if (result.Value.Sent < 0)
            return BadRequest(ApiResponse<object>.Error("Gmail is not connected — connect it first"));

        var (sent, failed) = result.Value;
        return Ok(ApiResponse<object>.Ok(new { sent, failed }));
    }

    /// <summary>Sent/failed email history recorded for this schedule.</summary>
    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetHistory(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var userId = GetUserId();
        var schedule = await _scheduleSvc.GetScheduleAsync(id, userId);
        if (schedule is null) return NotFound(ApiResponse<object>.Error("Schedule not found"));

        var (items, total) = await _scheduleSvc.GetHistoryAsync(id, userId, page, pageSize);
        var dtos = items.Select(m => new ScheduleHistoryItemDto
        {
            Id = m.Id,
            ToName = m.ToName,
            ToEmail = m.ToEmail,
            Status = m.Status,
            Error = m.Error,
            SentAt = m.SentAt,
            CreatedAt = m.CreatedAt,
        }).ToList();

        return Ok(ApiResponse<ScheduleHistoryResponseDto>.Ok(new ScheduleHistoryResponseDto
        {
            Items = dtos,
            Total = total,
            Page = page,
            PageSize = pageSize,
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateScheduleDto dto)
    {
        var userId = GetUserId();
        var cronError = ValidateCron(dto.CronExpression);
        if (cronError != null) return BadRequest(ApiResponse<EmailScheduleDto>.Error(cronError));

        var result = await _scheduleSvc.CreateScheduleAsync(userId, dto);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse<EmailScheduleDto>.Created(result));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScheduleDto dto)
    {
        var userId = GetUserId();
        if (dto.CronExpression != null)
        {
            var cronError = ValidateCron(dto.CronExpression);
            if (cronError != null) return BadRequest(ApiResponse<EmailScheduleDto>.Error(cronError));
        }

        var result = await _scheduleSvc.UpdateScheduleAsync(id, userId, dto);
        if (result is null) return NotFound(ApiResponse<EmailScheduleDto>.Error("Schedule not found"));
        return Ok(ApiResponse<EmailScheduleDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var deleted = await _scheduleSvc.DeleteScheduleAsync(id, userId);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Schedule not found"));
        return NoContent();
    }

    /// <summary>
    /// Applies a reusable ScheduleTemplate to one company, producing a concrete per-company
    /// EmailSchedule (own period/send time) plus a tracked Application with a SCHEDULED attempt.
    /// </summary>
    [HttpPost("apply-template")]
    public async Task<IActionResult> ApplyTemplate([FromBody] ApplyTemplateDto dto)
    {
        var userId = GetUserId();
        try
        {
            var result = await _scheduleSvc.ApplyTemplateAsync(userId, dto);
            return Ok(ApiResponse<ApplyTemplateResultDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ApplyTemplateResultDto>.Error(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ApplyTemplateResultDto>.Error(ex.Message));
        }
        catch (DuplicateApplicationException ex)
        {
            return Conflict(ApiResponse<ApplyTemplateResultDto>.Error("Duplicate application", ex.Payload));
        }
    }

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var userId = GetUserId();
        var toggled = await _scheduleSvc.ToggleScheduleAsync(id, userId);
        if (!toggled) return NotFound(ApiResponse<object>.Error("Schedule not found"));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>Cron must be parseable, otherwise the worker would silently never fire it.</summary>
    private static string? ValidateCron(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron)) return "Cron expression is required";
        try
        {
            Cronos.CronExpression.Parse(cron);
            return null;
        }
        catch (Exception)
        {
            return $"'{cron}' is not a valid cron expression";
        }
    }
}
