using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/email-schedules")]
public class EmailSchedulesController : ControllerBase
{
    private readonly EmailScheduleService _scheduleSvc;

    public EmailSchedulesController(EmailScheduleService scheduleSvc)
    {
        _scheduleSvc = scheduleSvc;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetAll(Guid userId)
    {
        var result = await _scheduleSvc.GetSchedulesAsync(userId);
        return Ok(ApiResponse<List<EmailScheduleDto>>.Ok(result));
    }

    [HttpGet("{userId}/{id}")]
    public async Task<IActionResult> Get(Guid userId, Guid id)
    {
        var result = await _scheduleSvc.GetScheduleAsync(id, userId);
        if (result is null) return NotFound(ApiResponse<EmailScheduleDto>.Error("Schedule not found"));
        return Ok(ApiResponse<EmailScheduleDto>.Ok(result));
    }

    [HttpPost("{userId}")]
    public async Task<IActionResult> Create(Guid userId, [FromBody] CreateScheduleDto dto)
    {
        var result = await _scheduleSvc.CreateScheduleAsync(userId, dto);
        return CreatedAtAction(nameof(Get), new { userId, id = result.Id }, ApiResponse<EmailScheduleDto>.Created(result));
    }

    [HttpPut("{userId}/{id}")]
    public async Task<IActionResult> Update(Guid userId, Guid id, [FromBody] UpdateScheduleDto dto)
    {
        var result = await _scheduleSvc.UpdateScheduleAsync(id, userId, dto);
        if (result is null) return NotFound(ApiResponse<EmailScheduleDto>.Error("Schedule not found"));
        return Ok(ApiResponse<EmailScheduleDto>.Ok(result));
    }

    [HttpDelete("{userId}/{id}")]
    public async Task<IActionResult> Delete(Guid userId, Guid id)
    {
        var deleted = await _scheduleSvc.DeleteScheduleAsync(id, userId);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Schedule not found"));
        return NoContent();
    }

    [HttpPatch("{userId}/{id}/toggle")]
    public async Task<IActionResult> Toggle(Guid userId, Guid id)
    {
        var toggled = await _scheduleSvc.ToggleScheduleAsync(id, userId);
        if (!toggled) return NotFound(ApiResponse<object>.Error("Schedule not found"));
        return Ok(ApiResponse<EmailScheduleDto>.Ok(await _scheduleSvc.GetScheduleAsync(id, userId)));
    }
}
