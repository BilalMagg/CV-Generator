using Microsoft.AspNetCore.Mvc;
using backend.src.features.experience.interfaces;
using backend.src.features.experience.dto;
using backend.src.shared.responses;

namespace backend.src.features.experience.controller;

[ApiController]
[Route("api/experiences")]
public class ExperienceController : ControllerBase
{
    private readonly IExperienceService _service;

    public ExperienceController(IExperienceService service)
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
    public async Task<IActionResult> Create([FromBody] CreateExperienceDto dto)
    {
        var result = await _service.Create(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Experience created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExperienceDto dto)
    {
        var result = await _service.Update(id, dto);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Experience deleted"));
    }
}
