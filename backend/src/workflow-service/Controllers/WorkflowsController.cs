using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService;
using WorkflowService.Entity;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(WorkflowDbContext db, ILogger<WorkflowsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var workflows = await _db.Workflows.ToListAsync();
        return Ok(ApiResponse<List<Workflow>>.Ok(workflows));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var workflow = await _db.Workflows.FindAsync(id);
        if (workflow == null) return NotFound(ApiResponse<Workflow>.Error("Workflow not found"));
        return Ok(ApiResponse<Workflow>.Ok(workflow));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDto dto)
    {
        var workflow = new Workflow
        {
            Name = dto.Name,
            Description = dto.Description,
            DefinitionJson = dto.DefinitionJson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created workflow {Id}", workflow.Id);
        return Created($"/api/workflows/{workflow.Id}", ApiResponse<Workflow>.Created(workflow));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowDto dto)
    {
        var workflow = await _db.Workflows.FindAsync(id);
        if (workflow == null) return NotFound(ApiResponse<Workflow>.Error("Workflow not found"));

        workflow.Name = dto.Name;
        workflow.Description = dto.Description;
        workflow.DefinitionJson = dto.DefinitionJson;
        workflow.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Workflow>.Ok(workflow));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var workflow = await _db.Workflows.FindAsync(id);
        if (workflow == null) return NotFound(ApiResponse<object>.Error("Workflow not found"));

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateWorkflowDto(string? Name, string? Description, string? DefinitionJson);
    public record UpdateWorkflowDto(string? Name, string? Description, string? DefinitionJson, bool IsActive);
}