using CVGenerator.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CvService.DTOs;
using CvService.Services;

namespace CvService.Controllers;

[ApiController]
[Route("api/cv/{cvId}/versions")]
[Authorize]
public class CvVersionsController : ControllerBase
{
    private readonly ICvVersionService _service;

    public CvVersionsController(ICvVersionService service)
    {
        _service = service;
    }

    /// GET /api/cv/{cvId}/versions
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid cvId)
    {
        var versions = await _service.GetByCvIdAsync(cvId, GetUserId());
        return Ok(ApiResponse<List<CvVersionDto>>.Ok(versions));
    }

    /// GET /api/cv/{cvId}/versions/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid cvId, Guid id)
    {
        var version = await _service.GetByIdAsync(id, GetUserId());
        if (version == null) return NotFound(ApiResponse<CvVersionDto>.Error("Version not found"));
        return Ok(ApiResponse<CvVersionDto>.Ok(version));
    }

    /// POST /api/cv/{cvId}/versions
    [HttpPost]
    public async Task<IActionResult> Create(Guid cvId, [FromBody] CreateCvVersionDto dto)
    {
        var created = await _service.CreateAsync(cvId, dto, GetUserId());
        return Created($"/api/cv/{cvId}/versions/{created.Id}", ApiResponse<CvVersionDto>.Created(created));
    }

    /// DELETE /api/cv/{cvId}/versions/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid cvId, Guid id)
    {
        var deleted = await _service.DeleteAsync(id, GetUserId());
        if (!deleted) return NotFound(ApiResponse<object>.Error("Version not found"));
        return NoContent();
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value
            ?? User.FindFirst("local_user_id")?.Value
            ?? throw new UnauthorizedAccessException();
    }
}
