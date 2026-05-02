using Microsoft.AspNetCore.Mvc;
using backend.src.features.skill.interfaces;
using backend.src.features.skill.dto;
using backend.src.shared.responses;

namespace backend.src.features.skill.controller;

[ApiController]
[Route("api/skills")]
public class SkillController : ControllerBase
{
    private readonly ISkillService _service;

    public SkillController(ISkillService service)
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
    public async Task<IActionResult> Create([FromBody] CreateSkillDto dto)
    {
        var result = await _service.Create(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Skill created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSkillDto dto)
    {
        var result = await _service.Update(id, dto);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Skill deleted"));
    }
}
