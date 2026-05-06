using CVGenerator.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CvService.Controllers;

[ApiController]
[Route("api/cv/templates")]
[Authorize]
public class CvTemplatesController : ControllerBase
{
    /// GET /api/cv/templates
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // TODO: Return available CV templates
        return Ok(ApiResponse<List<object>>.Ok(new List<object>()));
    }

    /// GET /api/cv/templates/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        // TODO: Return template details with preview
        return Ok(ApiResponse<object>.Ok(new { id, name = "Template preview" }));
    }
}
