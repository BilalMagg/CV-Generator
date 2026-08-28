using Microsoft.AspNetCore.Mvc;
using CV_Generator;
using CV_Generator.Dto;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[Route("api/email-schedules/templates")]
public class ScheduleTemplatesController : BaseApiController
{
    private readonly ScheduleTemplateService _svc;

    public ScheduleTemplatesController(ICurrentUserService currentUser, ScheduleTemplateService svc)
        : base(currentUser)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        var result = await _svc.GetAllAsync(userId);
        return Ok(ApiResponse<List<ScheduleTemplateDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetUserId();
        var result = await _svc.GetAsync(id, userId);
        if (result is null) return NotFound(ApiResponse<ScheduleTemplateDto>.Error("Template not found"));
        return Ok(ApiResponse<ScheduleTemplateDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateScheduleTemplateDto dto)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(ApiResponse<ScheduleTemplateDto>.Error("Name is required"));
        var result = await _svc.CreateAsync(userId, dto);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse<ScheduleTemplateDto>.Created(result));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScheduleTemplateDto dto)
    {
        var userId = GetUserId();
        var result = await _svc.UpdateAsync(id, userId, dto);
        if (result is null) return NotFound(ApiResponse<ScheduleTemplateDto>.Error("Template not found"));
        return Ok(ApiResponse<ScheduleTemplateDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var deleted = await _svc.DeleteAsync(id, userId);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Template not found"));
        return NoContent();
    }
}