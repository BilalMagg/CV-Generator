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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateScheduleDto dto)
    {
        var userId = GetUserId();
        var result = await _scheduleSvc.CreateScheduleAsync(userId, dto);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse<EmailScheduleDto>.Created(result));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScheduleDto dto)
    {
        var userId = GetUserId();
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

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var userId = GetUserId();
        var toggled = await _scheduleSvc.ToggleScheduleAsync(id, userId);
        if (!toggled) return NotFound(ApiResponse<object>.Error("Schedule not found"));
        return Ok(ApiResponse<EmailScheduleDto>.Ok(await _scheduleSvc.GetScheduleAsync(id, userId)));
    }
}
