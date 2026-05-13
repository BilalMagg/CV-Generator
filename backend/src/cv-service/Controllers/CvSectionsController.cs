using CVGenerator.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CvService.DTOs;
using CvService.Services;

namespace CvService.Controllers;

[ApiController]
[Route("api/cv/versions/{versionId}/sections")]
[Authorize]
public class CvSectionsController : ControllerBase
{
    private readonly ICvSectionService _service;

    public CvSectionsController(ICvSectionService service)
    {
        _service = service;
    }

    /// GET /api/cv/versions/{versionId}/sections
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid versionId)
    {
        var sections = await _service.GetByVersionIdAsync(versionId, GetUserId());
        return Ok(ApiResponse<List<CvSectionDto>>.Ok(sections));
    }

    /// GET /api/cv/versions/{versionId}/sections/{sectionType}
    [HttpGet("{sectionType}")]
    public async Task<IActionResult> GetByType(Guid versionId, string sectionType)
    {
        var section = await _service.GetByTypeAsync(versionId, sectionType, GetUserId());
        if (section == null) return NotFound(ApiResponse<CvSectionDto>.Error("Section not found"));
        return Ok(ApiResponse<CvSectionDto>.Ok(section));
    }

    /// PUT /api/cv/versions/{versionId}/sections/{sectionType}
    [HttpPut("{sectionType}")]
    public async Task<IActionResult> Upsert(Guid versionId, string sectionType, [FromBody] UpdateSectionDto dto)
    {
        var section = await _service.UpsertAsync(versionId, sectionType, dto, GetUserId());
        return Ok(ApiResponse<CvSectionDto>.Ok(section));
    }

    /// DELETE /api/cv/versions/{versionId}/sections/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid versionId, Guid id)
    {
        var deleted = await _service.DeleteAsync(id, GetUserId());
        if (!deleted) return NotFound(ApiResponse<object>.Error("Section not found"));
        return NoContent();
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value
            ?? User.FindFirst("local_user_id")?.Value
            ?? throw new UnauthorizedAccessException();
    }
}
