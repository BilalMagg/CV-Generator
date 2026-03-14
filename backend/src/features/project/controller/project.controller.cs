using Microsoft.AspNetCore.Mvc;
using backend.src.features.project.interfaces;
using backend.src.features.project.dto;
using backend.src.shared.responses;

namespace backend.src.features.project.controller;

[ApiController]
[Route("api/projects")]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _service;

    public ProjectController(IProjectService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAll();
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetById(id);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
    {
        var result = await _service.Create(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Project created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDto dto)
    {
        var result = await _service.Update(id, dto);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Project deleted"));
    }
}
