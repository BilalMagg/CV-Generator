using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService;
using WorkflowService.Entity;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(WorkflowDbContext db, ILogger<AgentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var agents = await _db.Agents.OrderBy(a => a.SortOrder).ToListAsync();
        return Ok(ApiResponse<List<AgentEntity>>.Ok(agents));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent == null) return NotFound(ApiResponse<AgentEntity>.Error("Agent not found"));
        return Ok(ApiResponse<AgentEntity>.Ok(agent));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAgentDto dto)
    {
        var agent = new AgentEntity
        {
            AgentId = dto.AgentId,
            Name = dto.Name,
            Role = dto.Role,
            BackgroundGradient = dto.BackgroundGradient,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
        };

        _db.Agents.Add(agent);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created agent {Id} ({Name})", agent.Id, agent.Name);
        return Created($"/api/agents/{agent.Id}", ApiResponse<AgentEntity>.Created(agent));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAgentDto dto)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent == null) return NotFound(ApiResponse<AgentEntity>.Error("Agent not found"));

        agent.AgentId = dto.AgentId;
        agent.Name = dto.Name;
        agent.Role = dto.Role;
        agent.BackgroundGradient = dto.BackgroundGradient;
        agent.SortOrder = dto.SortOrder;
        agent.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<AgentEntity>.Ok(agent));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent == null) return NotFound(ApiResponse<object>.Error("Agent not found"));

        _db.Agents.Remove(agent);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateAgentDto(string AgentId, string Name, string Role, string BackgroundGradient, int SortOrder, bool IsActive = true);
    public record UpdateAgentDto(string AgentId, string Name, string Role, string BackgroundGradient, int SortOrder, bool IsActive = true);
}
