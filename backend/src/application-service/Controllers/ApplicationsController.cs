using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApplicationService.DTOs;
using ApplicationService.Services;
using ApplicationService.Validators;
using FluentValidation;

namespace ApplicationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _service;
    private readonly IValidator<CreateApplicationDto> _createValidator;
    private readonly IValidator<UpdateStatusDto> _statusValidator;
    private readonly ILogger<ApplicationsController> _logger;

    public ApplicationsController(
        IApplicationService service,
        IValidator<CreateApplicationDto> createValidator,
        IValidator<UpdateStatusDto> statusValidator,
        ILogger<ApplicationsController> logger)
    {
        _service = service;
        _createValidator = createValidator;
        _statusValidator = statusValidator;
        _logger = logger;
    }

    private string? GetUserId()
    {
        return User.FindFirst("sub")?.Value
            ?? User.FindFirst("local_user_id")?.Value;
    }

    /// GET /applications
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? candidateId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _service.GetAllAsync(candidateId, page, pageSize);
        return Ok(result);
    }

    /// GET /applications/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var app = await _service.GetByIdAsync(id);
        if (app == null) return NotFound(new { message = "Application not found", statusCode = 404 });
        return Ok(app);
    }

    /// POST /applications
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { message = validation.Errors.First().ErrorMessage, statusCode = 400 });

        var created = await _service.CreateAsync(dto, GetUserId());
        return Created($"/api/applications/{created.Id}", created);
    }

    /// PATCH /applications/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var validation = await _statusValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { message = validation.Errors.First().ErrorMessage, statusCode = 400 });

        var updated = await _service.UpdateStatusAsync(id, dto, GetUserId());
        if (updated == null) return NotFound(new { message = "Application not found", statusCode = 404 });
        return Ok(updated);
    }

    /// DELETE /applications/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = "Application not found", statusCode = 404 });
        return NoContent();
    }

    /// GET /applications/statistics
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] Guid? candidateId)
    {
        var stats = await _service.GetStatisticsAsync(candidateId);
        return Ok(stats);
    }
}