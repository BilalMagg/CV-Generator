using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Services;

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

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid userId)
    {
        var result = await _scheduleSvc.GetSchedulesAsync(userId);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid userId)
    {
        var result = await _scheduleSvc.GetScheduleAsync(id, userId);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromQuery] Guid userId, [FromBody] CreateScheduleDto dto)
    {
        var result = await _scheduleSvc.CreateScheduleAsync(userId, dto);
        return CreatedAtAction(nameof(Get), new { id = result.Id, userId }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromQuery] Guid userId, [FromBody] UpdateScheduleDto dto)
    {
        var result = await _scheduleSvc.UpdateScheduleAsync(id, userId, dto);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid userId)
    {
        var deleted = await _scheduleSvc.DeleteScheduleAsync(id, userId);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, [FromQuery] Guid userId)
    {
        var toggled = await _scheduleSvc.ToggleScheduleAsync(id, userId);
        if (!toggled) return NotFound();
        return Ok(new { toggled = true });
    }
}
