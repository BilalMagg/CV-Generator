using Microsoft.AspNetCore.Mvc;
using backend.src.features.workflow.interfaces;
using backend.src.features.workflow.dto;
using backend.src.shared.responses;

namespace backend.src.features.workflow.controller;

[ApiController]
[Route("api/workflows")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _service;

    public WorkflowController(IWorkflowService service)
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
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDto dto)
    {
        var result = await _service.Create(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Workflow created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowDto dto)
    {
        var result = await _service.Update(id, dto);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Workflow deleted"));
    }
}
