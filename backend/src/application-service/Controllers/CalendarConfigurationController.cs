using System.Security.Claims;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApplicationService.DTOs;
using ApplicationService.Services;

namespace ApplicationService.Controllers;

[ApiController]
[Authorize]
[Route("api/applications/calendar-configuration")]
public class CalendarConfigurationController : ControllerBase
{
    private readonly ICalendarConfigurationService _service;
    private readonly ILogger<CalendarConfigurationController> _logger;

    public CalendarConfigurationController(
        ICalendarConfigurationService service,
        ILogger<CalendarConfigurationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("local_user_id")?.Value;
        if (sub == null) return null;
        return Guid.TryParse(sub, out var guid) ? guid : null;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var config = await _service.GetAsync(userId.Value);
        if (config == null)
        {
            var defaults = new CalendarConfigurationDto(
                Guid.Empty,
                userId.Value,
                true,
                new[] { "PENDING", "INTERVIEW" }
            );
            return Ok(ApiResponse<CalendarConfigurationDto>.Ok(defaults));
        }

        return Ok(ApiResponse<CalendarConfigurationDto>.Ok(config));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateCalendarConfigurationDto dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var config = await _service.UpdateAsync(userId.Value, dto);
        return Ok(ApiResponse<CalendarConfigurationDto>.Ok(config));
    }
}
