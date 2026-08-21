using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CV_Generator;
using CV_Generator.Dto;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/cv/templates")]
[Authorize]
public class CvTemplatesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(ApiResponse<List<object>>.Ok(new List<object>()));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        return Ok(ApiResponse<object>.Ok(new { id, name = "Template preview" }));
    }
}
