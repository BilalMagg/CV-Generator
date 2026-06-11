using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService;
using WorkflowService.Entity;
using WorkflowService.AgentClients;
using WorkflowService.Models;
using System.Text.Json;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<AgentsController> _logger;
    private readonly ISearchAgentClient _searchAgentClient;

    public AgentsController(WorkflowDbContext db, ILogger<AgentsController> logger, ISearchAgentClient searchAgentClient)
    {
        _db = db;
        _logger = logger;
        _searchAgentClient = searchAgentClient;
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

    [HttpPost("search")]
    public async Task<IActionResult> SearchCandidates([FromBody] SearchAgentFrontendRequest request)
    {
        // Try to get internal User ID from API Gateway header first
        var userIdString = Request.Headers["X-User-Id"].FirstOrDefault();
        
        // Fallback to JWT 'sub' claim or a hardcoded demo ID
        if (string.IsNullOrEmpty(userIdString))
        {
            userIdString = User.FindFirst("sub")?.Value;
        }

        var userId = Guid.TryParse(userIdString, out var uid) ? uid : Guid.Parse("34f9a4d3-b491-4ee4-946a-b04d37788ff8");

        var jobRequirements = new JobRequirements();

        if (request.ExtractedJobId.HasValue)
        {
            var extraction = await _db.JobExtractions.FindAsync(request.ExtractedJobId.Value);
            if (extraction == null) return NotFound(ApiResponse<object>.Error("Extracted job not found"));

            try
            {
                var extractedOutput = JsonSerializer.Deserialize<ExtractorOutput>(extraction.OutputJson);
                if (extractedOutput != null)
                {
                    jobRequirements.JobRole = extractedOutput.JobRole;
                    jobRequirements.ExtractedSkills = extractedOutput.RequiredSkills;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse extraction output json");
            }
        }
        else if (!string.IsNullOrEmpty(request.Text))
        {
            jobRequirements.JobRole = "Candidate Search"; // Default
            jobRequirements.Keywords = request.Text.Split(new[] { ' ', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        else if (!string.IsNullOrEmpty(request.Keywords))
        {
            jobRequirements.JobRole = "Candidate Search"; // Default
            jobRequirements.Keywords = request.Keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList();
        }
        else
        {
            return BadRequest(ApiResponse<object>.Error("You must provide either an ExtractedJobId, Text, or Keywords"));
        }

        var searchInput = new SearchInput
        {
            UserId = userId,
            JobRequirements = jobRequirements
        };

        try
        {
            var result = await _searchAgentClient.MatchAsync(searchInput);
            if (result == null) return StatusCode(500, ApiResponse<object>.Error("Search Agent returned null"));
            return Ok(ApiResponse<SearchOutput>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Search Agent");
            return StatusCode(500, ApiResponse<object>.Error($"Search failed: {ex.Message}"));
        }
    }
}
