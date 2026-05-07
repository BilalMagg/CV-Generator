using CVGenerator.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CvService.DTOs;
using CvService.Services;

namespace CvService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CvsController : ControllerBase
{
    private readonly ICvService _service;

    public CvsController(ICvService service)
    {
        _service = service;
    }

    /// GET /api/cv
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cvs = await _service.GetAllAsync(GetUserId());
        return Ok(ApiResponse<List<CvDto>>.Ok(cvs));
    }

    /// GET /api/cv/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cv = await _service.GetByIdAsync(id, GetUserId());
        if (cv == null) return NotFound(ApiResponse<CvDto>.Error("CV not found"));
        return Ok(ApiResponse<CvDto>.Ok(cv));
    }

    /// POST /api/cv
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCvDto dto)
    {
        var created = await _service.CreateAsync(dto, GetUserId());
        return Created($"/api/cv/{created.Id}", ApiResponse<CvDto>.Created(created));
    }

    /// PUT /api/cv/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCvDto dto)
    {
        var updated = await _service.UpdateAsync(id, dto, GetUserId());
        if (updated == null) return NotFound(ApiResponse<CvDto>.Error("CV not found"));
        return Ok(ApiResponse<CvDto>.Ok(updated));
    }

    /// DELETE /api/cv/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id, GetUserId());
        if (!deleted) return NotFound(ApiResponse<object>.Error("CV not found"));
        return NoContent();
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value
            ?? User.FindFirst("local_user_id")?.Value
            ?? throw new UnauthorizedAccessException();
    }
}
