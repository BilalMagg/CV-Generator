using System.ComponentModel.DataAnnotations;
using CVGenerator.Shared;
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
    private readonly IValidator<UpdateApplicationDto> _updateValidator;
    private readonly ILogger<ApplicationsController> _logger;

    public ApplicationsController(
        IApplicationService service,
        IValidator<CreateApplicationDto> createValidator,
        IValidator<UpdateStatusDto> statusValidator,
        IValidator<UpdateApplicationDto> updateValidator,
        ILogger<ApplicationsController> logger)
    {
        _service = service;
        _createValidator = createValidator;
        _statusValidator = statusValidator;
        _updateValidator = updateValidator;
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
        return Ok(ApiResponse<ApplicationListDto>.Ok(result));
    }

    /// GET /applications/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var app = await _service.GetByIdAsync(id);
        if (app == null) return NotFound(ApiResponse<ApplicationResponseDto>.Error("Application not found"));
        return Ok(ApiResponse<ApplicationResponseDto>.Ok(app));
    }

    /// POST /applications
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<ApplicationResponseDto>.Error(validation.Errors.First().ErrorMessage));

        var created = await _service.CreateAsync(dto, GetUserId());
        return Created($"/api/applications/{created.Id}", ApiResponse<ApplicationResponseDto>.Created(created));
    }

    /// PATCH /applications/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
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
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Application not found"));
        return NoContent();
    }

    /// GET /applications/statistics
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] Guid? candidateId)
    {
        var stats = await _service.GetStatisticsAsync(candidateId);
        return Ok(ApiResponse<ApplicationStatisticsDto>.Ok(stats));
    }
}