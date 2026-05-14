˘
</app/src/workflow-service/AgentClients/ContactAgentClient.cs£using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface IContactAgentClient
{
    Task<ContactOutput?> DeliverAsync(ContactInput input);
    Task<bool> CheckHealthAsync();
}

public class ContactAgentClient : IContactAgentClient
{
    private readonly HttpClient _client;

    public ContactAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<ContactOutput?> DeliverAsync(ContactInput input)
    {
        var response = await _client.PostAsJsonAsync("deliver", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContactOutput>();
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _client.GetAsync("health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
ParseOptions.0.jsonÅ
;/app/src/workflow-service/AgentClients/CvOptimizerClient.cs¨using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ICvOptimizerClient
{
    Task<OptimizerOutput?> OptimizeAsync(OptimizerInput input);
    Task<bool> CheckHealthAsync();
}

public class CvOptimizerClient : ICvOptimizerClient
{
    private readonly HttpClient _client;

    public CvOptimizerClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<OptimizerOutput?> OptimizeAsync(OptimizerInput input)
    {
        var response = await _client.PostAsJsonAsync("optimize", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OptimizerOutput>();
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _client.GetAsync("health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
ParseOptions.0.jsonÉ
</app/src/workflow-service/AgentClients/JobExtractorClient.cs≠using WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface IJobExtractorClient
{
    Task<ExtractorOutput?> ExtractAsync(ExtractorInput input);
    Task<bool> CheckHealthAsync();
}

public class JobExtractorClient : IJobExtractorClient
{
    private readonly HttpClient _client;

    public JobExtractorClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<ExtractorOutput?> ExtractAsync(ExtractorInput input)
    {
        var response = await _client.PostAsJsonAsync("extract", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExtractorOutput>();
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _client.GetAsync("health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
ParseOptions.0.jsonÈ
;/app/src/workflow-service/AgentClients/SearchAgentClient.csîusing WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ISearchAgentClient
{
    Task<SearchOutput?> MatchAsync(SearchInput input);
    Task<bool> CheckHealthAsync();
}

public class SearchAgentClient : ISearchAgentClient
{
    private readonly HttpClient _client;

    public SearchAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<SearchOutput?> MatchAsync(SearchInput input)
    {
        var response = await _client.PostAsJsonAsync("match", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SearchOutput>();
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _client.GetAsync("health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
ParseOptions.0.jsonÙ
=/app/src/workflow-service/AgentClients/TemplateAgentClient.csùusing WorkflowService.Models;

namespace WorkflowService.AgentClients;

public interface ITemplateAgentClient
{
    Task<RenderedCV?> RenderAsync(TemplateInput input);
    Task<bool> CheckHealthAsync();
}

public class TemplateAgentClient : ITemplateAgentClient
{
    private readonly HttpClient _client;

    public TemplateAgentClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<RenderedCV?> RenderAsync(TemplateInput input)
    {
        var response = await _client.PostAsJsonAsync("render", input);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RenderedCV>();
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _client.GetAsync("health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
ParseOptions.0.jsonª
>/app/src/workflow-service/Controllers/ExperiencesController.cs„using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService.Entity;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExperiencesController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<ExperiencesController> _logger;

    public ExperiencesController(WorkflowDbContext db, ILogger<ExperiencesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var experiences = userId.HasValue
            ? await _db.Experiences.Where(e => e.UserId == userId.Value).ToListAsync()
            : await _db.Experiences.ToListAsync();
        return Ok(ApiResponse<List<Experience>>.Ok(experiences));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var exp = await _db.Experiences.FindAsync(id);
        if (exp == null) return NotFound(ApiResponse<Experience>.Error("Experience not found"));
        return Ok(ApiResponse<Experience>.Ok(exp));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExperienceDto dto)
    {
        var exp = new Experience
        {
            Title = dto.Title,
            Company = dto.Company,
            Description = dto.Description,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ReferenceUrl = dto.ReferenceUrl,
            Status = dto.Status,
            UserId = dto.UserId
        };

        _db.Experiences.Add(exp);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created experience {Id}", exp.Id);
        return Created($"/api/experiences/{exp.Id}", ApiResponse<Experience>.Created(exp));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExperienceDto dto)
    {
        var exp = await _db.Experiences.FindAsync(id);
        if (exp == null) return NotFound(ApiResponse<Experience>.Error("Experience not found"));

        exp.Title = dto.Title;
        exp.Company = dto.Company;
        exp.Description = dto.Description;
        exp.StartDate = dto.StartDate;
        exp.EndDate = dto.EndDate;
        exp.ReferenceUrl = dto.ReferenceUrl;
        exp.Status = dto.Status;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Experience>.Ok(exp));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var exp = await _db.Experiences.FindAsync(id);
        if (exp == null) return NotFound(ApiResponse<object>.Error("Experience not found"));

        _db.Experiences.Remove(exp);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateExperienceDto(
        string Title,
        string? Company,
        string? Description,
        DateTime StartDate,
        DateTime? EndDate,
        string? ReferenceUrl,
        string Status,
        Guid UserId
    );

    public record UpdateExperienceDto(
        string Title,
        string? Company,
        string? Description,
        DateTime StartDate,
        DateTime? EndDate,
        string? ReferenceUrl,
        string Status
    );
}
ParseOptions.0.jsonÕ
;/app/src/workflow-service/Controllers/ProjectsController.cs¯using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService.Entity;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(WorkflowDbContext db, ILogger<ProjectsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var projects = userId.HasValue
            ? await _db.Projects.Where(p => p.UserId == userId.Value).ToListAsync()
            : await _db.Projects.ToListAsync();
        return Ok(ApiResponse<List<Project>>.Ok(projects));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResponse<Project>.Error("Project not found"));
        return Ok(ApiResponse<Project>.Ok(project));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
    {
        var project = new Project
        {
            Title = dto.Title,
            Description = dto.Description,
            Role = dto.Role,
            Achievements = dto.Achievements,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            RepositoryUrl = dto.RepositoryUrl,
            DemoUrl = dto.DemoUrl,
            Status = dto.Status,
            UserId = dto.UserId,
            SkillsJson = dto.SkillsJson
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created project {Id}", project.Id);
        return Created($"/api/projects/{project.Id}", ApiResponse<Project>.Created(project));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDto dto)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResponse<Project>.Error("Project not found"));

        project.Title = dto.Title;
        project.Description = dto.Description;
        project.Role = dto.Role;
        project.Achievements = dto.Achievements;
        project.StartDate = dto.StartDate;
        project.EndDate = dto.EndDate;
        project.RepositoryUrl = dto.RepositoryUrl;
        project.DemoUrl = dto.DemoUrl;
        project.Status = dto.Status;
        project.SkillsJson = dto.SkillsJson;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Project>.Ok(project));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResponse<object>.Error("Project not found"));

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateProjectDto(
        string Title,
        string? Description,
        string? Role,
        string? Achievements,
        DateTime StartDate,
        DateTime? EndDate,
        string? RepositoryUrl,
        string? DemoUrl,
        string Status,
        Guid UserId,
        string? SkillsJson
    );

    public record UpdateProjectDto(
        string Title,
        string? Description,
        string? Role,
        string? Achievements,
        DateTime StartDate,
        DateTime? EndDate,
        string? RepositoryUrl,
        string? DemoUrl,
        string Status,
        string? SkillsJson
    );
}
ParseOptions.0.jsonˆ
9/app/src/workflow-service/Controllers/SkillsController.cs£using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService.Entity;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkillsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<SkillsController> _logger;

    public SkillsController(WorkflowDbContext db, ILogger<SkillsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        var skills = userId.HasValue
            ? await _db.Skills.Where(s => s.UserId == userId.Value).ToListAsync()
            : await _db.Skills.ToListAsync();
        return Ok(ApiResponse<List<Skill>>.Ok(skills));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<Skill>.Error("Skill not found"));
        return Ok(ApiResponse<Skill>.Ok(skill));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSkillDto dto)
    {
        var skill = new Skill
        {
            Name = dto.Name,
            Level = dto.Level,
            YearsOfExperience = dto.YearsOfExperience,
            UserId = dto.UserId,
            Category = dto.Category
        };

        _db.Skills.Add(skill);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created skill {Id}", skill.Id);
        return Created($"/api/skills/{skill.Id}", ApiResponse<Skill>.Created(skill));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSkillDto dto)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<Skill>.Error("Skill not found"));

        skill.Name = dto.Name;
        skill.Level = dto.Level;
        skill.YearsOfExperience = dto.YearsOfExperience;
        skill.Category = dto.Category;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<Skill>.Ok(skill));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill == null) return NotFound(ApiResponse<object>.Error("Skill not found"));

        _db.Skills.Remove(skill);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateSkillDto(string Name, string? Level, int? YearsOfExperience, Guid UserId, string? Category);
    public record UpdateSkillDto(string Name, string? Level, int? YearsOfExperience, string? Category);
}
ParseOptions.0.jsonÓ
?/app/src/workflow-service/Controllers/VectorSearchController.csïusing CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using WorkflowService.Entity;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/vectors")]
public class VectorSearchController : ControllerBase
{
    private readonly WorkflowDbContext _db;

    public VectorSearchController(WorkflowDbContext db)
    {
        _db = db;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncVectors([FromBody] SyncVectorsRequest req)
    {
        // Delete existing vectors for this user to do a fresh sync
        var existing = await _db.AgentDocumentChunks
            .Where(x => x.UserId == req.UserId)
            .ToListAsync();
            
        _db.AgentDocumentChunks.RemoveRange(existing);

        var newChunks = req.Chunks.Select(c => new AgentDocumentChunk
        {
            UserId = req.UserId,
            SourceType = c.SourceType,
            SourceId = c.SourceId,
            Content = c.Content,
            Embedding = new Vector(c.Embedding)
        });

        _db.AgentDocumentChunks.AddRange(newChunks);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] VectorSearchRequest req)
    {
        var queryVector = new Vector(req.QueryVector);
        
        // BMO 70/30 Hybrid Search combining semantic similarity and exact keyword match.
        // It uses FromSqlRaw to execute the PostgreSQL specific operations securely.
        var sqlQuery = @"
            SELECT *
            FROM ""AgentDocumentChunks""
            WHERE ""UserId"" = {2}
            ORDER BY 
                (COALESCE((1 - (""Embedding"" <=> {0})), 0) * 0.7) +
                (COALESCE(ts_rank_cd(""SearchVector"", plainto_tsquery('english', {1})), 0) * 0.3) DESC
            LIMIT {3}
        ";

        var results = await _db.AgentDocumentChunks
            .FromSqlRaw(sqlQuery, queryVector, req.QueryText, req.UserId, req.Limit)
            .Select(x => new VectorSearchResult(x.SourceId, x.SourceType))
            .ToListAsync();

        return Ok(ApiResponse<List<VectorSearchResult>>.Ok(results));
    }

    [HttpGet("status/{userId}")]
    public async Task<IActionResult> GetStatus(Guid userId)
    {
        var hasVectors = await _db.AgentDocumentChunks.AnyAsync(x => x.UserId == userId);
        return Ok(ApiResponse<bool>.Ok(hasVectors));
    }
}

public record SyncVectorsRequest(Guid UserId, List<DocumentChunkDto> Chunks);
public record DocumentChunkDto(string SourceType, Guid SourceId, string Content, float[] Embedding);

public record VectorSearchRequest(Guid UserId, string QueryText, float[] QueryVector, int Limit = 15);
public record VectorSearchResult(Guid SourceId, string SourceType);
ParseOptions.0.jsonÉ
</app/src/workflow-service/Controllers/WorkflowsController.cs≠using CVGenerator.Shared;
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

    [HttpPost("generate-cv")]
    public async Task<IActionResult> GenerateTailoredCv([FromBody] WorkflowService.Models.GenerateCvRequest request, [FromServices] WorkflowService.Services.WorkflowExecutionService executionService)
    {
        if (request == null || request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.JobDescription))
            return BadRequest(ApiResponse<object>.Error("Invalid request payload. UserId and JobDescription are required."));

        try
        {
            var finalResult = await executionService.GenerateTailoredCvAsync(request);
            return Ok(ApiResponse<WorkflowService.Models.ContactOutput>.Ok(finalResult));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating tailored CV for User {UserId}", request.UserId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    public record CreateWorkflowDto(string? Name, string? Description, string? DefinitionJson);
    public record UpdateWorkflowDto(string? Name, string? Description, string? DefinitionJson, bool IsActive);
}ParseOptions.0.jsonÿ
6/app/src/workflow-service/Entity/AgentDocumentChunk.csàusing System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;
using Pgvector;

namespace WorkflowService.Entity;

public class AgentDocumentChunk
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Column(TypeName = "vector(768)")]
    public Vector? Embedding { get; set; }

    public NpgsqlTsVector? SearchVector { get; set; }
}
ParseOptions.0.jsonÍ
./app/src/workflow-service/Entity/Experience.cs¢using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace WorkflowService.Entity;

[Table("experiences")]
public class Experience
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public required string Title { get; set; }

    [MaxLength(150)]
    public string? Company { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? ReferenceUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Ongoing";

    [Required]
    public Guid UserId { get; set; }

    public string? AiSummaryJson { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? DescriptionEmbedding { get; set; }
}
ParseOptions.0.jsonè	
+/app/src/workflow-service/Entity/Project.cs using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace WorkflowService.Entity;

[Table("projects")]
public class Project
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public required string Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Role { get; set; }

    [MaxLength(1000)]
    public string? Achievements { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [MaxLength(300)]
    public string? RepositoryUrl { get; set; }

    [MaxLength(300)]
    public string? DemoUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Ongoing";

    [Required]
    public Guid UserId { get; set; }

    public string? SkillsJson { get; set; }
    public string? AiSummaryJson { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? DescriptionEmbedding { get; set; }
}
ParseOptions.0.json…
)/app/src/workflow-service/Entity/Skill.csÜusing System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace WorkflowService.Entity;

[Table("skills")]
public class Skill
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(20)]
    public string? Level { get; set; }

    public int? YearsOfExperience { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    [Column(TypeName = "vector(384)")]
    public Vector? NameEmbedding { get; set; }
}
ParseOptions.0.jsonÏ
,/app/src/workflow-service/Entity/Workflow.cs¶using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkflowService.Entity;

[Table("workflows")]
public class Workflow
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? DefinitionJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}ParseOptions.0.json»
5/app/src/workflow-service/Grpc/WorkflowServiceImpl.cs˘using Grpc.Core;
using Grpc.Net.Client;
using CommonProtos.Workflow;
using Microsoft.EntityFrameworkCore;
using WorkflowService;
using WorkflowService.Entity;

namespace WorkflowService.Grpc;

public class WorkflowServiceImpl : CommonProtos.Workflow.WorkflowServiceGrpc.WorkflowServiceGrpcBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<WorkflowServiceImpl> _logger;

    public WorkflowServiceImpl(WorkflowDbContext db, ILogger<WorkflowServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<WorkflowProto> GetWorkflowById(GetWorkflowByIdRequest request, ServerCallContext context)
    {
        var workflow = await _db.Workflows.FindAsync(Guid.Parse(request.Id));
        if (workflow == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Workflow not found"));

        return ToProto(workflow);
    }

    public override async Task GetAllWorkflows(GetAllWorkflowsRequest request, IServerStreamWriter<WorkflowProto> responseStream, ServerCallContext context)
    {
        var workflows = await _db.Workflows.ToListAsync();
        foreach (var workflow in workflows)
        {
            await responseStream.WriteAsync(ToProto(workflow));
        }
    }

    public override async Task<WorkflowProto> CreateWorkflow(CreateWorkflowRequest request, ServerCallContext context)
    {
        var workflow = new Workflow
        {
            Name = request.Name,
            Description = request.Description,
            DefinitionJson = request.DefinitionJson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created workflow {Id}", workflow.Id);
        return ToProto(workflow);
    }

    public override async Task<WorkflowProto> UpdateWorkflow(UpdateWorkflowRequest request, ServerCallContext context)
    {
        var workflow = await _db.Workflows.FindAsync(Guid.Parse(request.Id));
        if (workflow == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Workflow not found"));

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.DefinitionJson = request.DefinitionJson;
        workflow.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return ToProto(workflow);
    }

    public override async Task<DeleteWorkflowResponse> DeleteWorkflow(DeleteWorkflowRequest request, ServerCallContext context)
    {
        var workflow = await _db.Workflows.FindAsync(Guid.Parse(request.Id));
        if (workflow == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Workflow not found"));

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync();

        return new DeleteWorkflowResponse { Success = true };
    }

    private static WorkflowProto ToProto(Workflow w) => new()
    {
        Id = w.Id.ToString(),
        Name = w.Name ?? "",
        Description = w.Description ?? "",
        DefinitionJson = w.DefinitionJson ?? "",
        IsActive = w.IsActive,
        CreatedAt = w.CreatedAt.ToString("O")
    };
}ParseOptions.0.json◊
F/app/src/workflow-service/Migrations/20260506170039_AddAgentVectors.cs˜using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace WorkflowService.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentVectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "AgentDocumentChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "Content" })
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDocumentChunks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefinitionJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDocumentChunks_SearchVector",
                table: "AgentDocumentChunks",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentDocumentChunks");

            migrationBuilder.DropTable(
                name: "workflows");
        }
    }
}
ParseOptions.0.jsonÖ
O/app/src/workflow-service/Migrations/20260506170039_AddAgentVectors.Designer.csú// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using Pgvector;
using WorkflowService;

#nullable disable

namespace WorkflowService.Migrations
{
    [DbContext(typeof(WorkflowDbContext))]
    [Migration("20260506170039_AddAgentVectors")]
    partial class AddAgentVectors
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("WorkflowService.Entity.AgentDocumentChunk", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Vector>("Embedding")
                        .HasColumnType("vector(768)");

                    b.Property<NpgsqlTsVector>("SearchVector")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("tsvector")
                        .HasAnnotation("Npgsql:TsVectorConfig", "english")
                        .HasAnnotation("Npgsql:TsVectorProperties", new[] { "Content" });

                    b.Property<Guid>("SourceId")
                        .HasColumnType("uuid");

                    b.Property<string>("SourceType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("SearchVector");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("SearchVector"), "GIN");

                    b.ToTable("AgentDocumentChunks");
                });

            modelBuilder.Entity("WorkflowService.Entity.Workflow", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("DefinitionJson")
                        .HasColumnType("text");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.ToTable("workflows");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.jsonœ'
S/app/src/workflow-service/Migrations/20260507003001_MigrateUserContentToWorkflow.cs‚&using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace WorkflowService.Migrations
{
    /// <inheritdoc />
    public partial class MigrateUserContentToWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "experiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Company = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiSummaryJson = table.Column<string>(type: "text", nullable: true),
                    DescriptionEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Achievements = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RepositoryUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DemoUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillsJson = table.Column<string>(type: "text", nullable: true),
                    AiSummaryJson = table.Column<string>(type: "text", nullable: true),
                    DescriptionEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NameEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experiences");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "skills");
        }
    }
}
ParseOptions.0.json∆F
\/app/src/workflow-service/Migrations/20260507003001_MigrateUserContentToWorkflow.Designer.cs–E// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using Pgvector;
using WorkflowService;

#nullable disable

namespace WorkflowService.Migrations
{
    [DbContext(typeof(WorkflowDbContext))]
    [Migration("20260507003001_MigrateUserContentToWorkflow")]
    partial class MigrateUserContentToWorkflow
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("WorkflowService.Entity.AgentDocumentChunk", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Vector>("Embedding")
                        .HasColumnType("vector(768)");

                    b.Property<NpgsqlTsVector>("SearchVector")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("tsvector")
                        .HasAnnotation("Npgsql:TsVectorConfig", "english")
                        .HasAnnotation("Npgsql:TsVectorProperties", new[] { "Content" });

                    b.Property<Guid>("SourceId")
                        .HasColumnType("uuid");

                    b.Property<string>("SourceType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("SearchVector");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("SearchVector"), "GIN");

                    b.ToTable("AgentDocumentChunks");
                });

            modelBuilder.Entity("WorkflowService.Entity.Experience", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("Company")
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ReferenceUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.ToTable("experiences");
                });

            modelBuilder.Entity("WorkflowService.Entity.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Achievements")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("DemoUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("RepositoryUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Role")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("SkillsJson")
                        .HasColumnType("text");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.ToTable("projects");
                });

            modelBuilder.Entity("WorkflowService.Entity.Skill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Category")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Level")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Vector>("NameEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<int?>("YearsOfExperience")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.ToTable("skills");
                });

            modelBuilder.Entity("WorkflowService.Entity.Workflow", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("DefinitionJson")
                        .HasColumnType("text");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.ToTable("workflows");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.jsonØE
F/app/src/workflow-service/Migrations/WorkflowDbContextModelSnapshot.csœD// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using Pgvector;
using WorkflowService;

#nullable disable

namespace WorkflowService.Migrations
{
    [DbContext(typeof(WorkflowDbContext))]
    partial class WorkflowDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("WorkflowService.Entity.AgentDocumentChunk", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Vector>("Embedding")
                        .HasColumnType("vector(768)");

                    b.Property<NpgsqlTsVector>("SearchVector")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("tsvector")
                        .HasAnnotation("Npgsql:TsVectorConfig", "english")
                        .HasAnnotation("Npgsql:TsVectorProperties", new[] { "Content" });

                    b.Property<Guid>("SourceId")
                        .HasColumnType("uuid");

                    b.Property<string>("SourceType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("SearchVector");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("SearchVector"), "GIN");

                    b.ToTable("AgentDocumentChunks");
                });

            modelBuilder.Entity("WorkflowService.Entity.Experience", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("Company")
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ReferenceUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.ToTable("experiences");
                });

            modelBuilder.Entity("WorkflowService.Entity.Project", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Achievements")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<string>("AiSummaryJson")
                        .HasColumnType("text");

                    b.Property<string>("DemoUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<Vector>("DescriptionEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("RepositoryUrl")
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)");

                    b.Property<string>("Role")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("SkillsJson")
                        .HasColumnType("text");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.ToTable("projects");
                });

            modelBuilder.Entity("WorkflowService.Entity.Skill", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Category")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<string>("Level")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<Vector>("NameEmbedding")
                        .HasColumnType("vector(384)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<int?>("YearsOfExperience")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.ToTable("skills");
                });

            modelBuilder.Entity("WorkflowService.Entity.Workflow", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("DefinitionJson")
                        .HasColumnType("text");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.ToTable("workflows");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.jsonÈ+
//app/src/workflow-service/Models/AgentModels.cs†+using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WorkflowService.Models;

// Generic JobRequirements used by JobExtractor and SearchAgent
public class JobRequirements
{
    [JsonPropertyName("job_role")]
    public string? JobRole { get; set; }

    [JsonPropertyName("extracted_skills")]
    public List<string> ExtractedSkills { get; set; } = new();

    [JsonPropertyName("required_experience_years")]
    public int RequiredExperienceYears { get; set; }

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("seniority_level")]
    public string? SeniorityLevel { get; set; }

    [JsonPropertyName("employment_type")]
    public string? EmploymentType { get; set; }

    [JsonPropertyName("location_type")]
    public string? LocationType { get; set; }

    [JsonPropertyName("responsibilities")]
    public List<string> Responsibilities { get; set; } = new();

    [JsonPropertyName("certifications")]
    public List<string> Certifications { get; set; } = new();
}

// Job Extractor
public class ExtractorInput
{
    [JsonPropertyName("job_description")]
    public string JobDescription { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";
}

public class ExtractorOutput : JobRequirements { }

// Search Agent
public class SearchInput
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("job_requirements")]
    public JobRequirements JobRequirements { get; set; } = new();
}

public class SearchOutput
{
    [JsonPropertyName("matched_skills")]
    public List<dynamic> MatchedSkills { get; set; } = new();

    [JsonPropertyName("matched_experiences")]
    public List<dynamic> MatchedExperiences { get; set; } = new();

    [JsonPropertyName("matched_projects")]
    public List<dynamic> MatchedProjects { get; set; } = new();

    [JsonPropertyName("gap_skills")]
    public List<string> GapSkills { get; set; } = new();

    [JsonPropertyName("match_score")]
    public double MatchScore { get; set; }
}

// CV Optimizer
public class OptimizerInput
{
    [JsonPropertyName("job_data")]
    public string JobData { get; set; } = string.Empty;

    [JsonPropertyName("candidate_name")]
    public string CandidateName { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("user_focus")]
    public string? UserFocus { get; set; }
}

public class OptimizerOutput
{
    [JsonPropertyName("ats_score_before")]
    public int AtsScoreBefore { get; set; }

    [JsonPropertyName("ats_score_after")]
    public int AtsScoreAfter { get; set; }

    [JsonPropertyName("improvement")]
    public int Improvement { get; set; }

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;
}

// Template Agent
public class TemplateInput
{
    [JsonPropertyName("cv_draft")]
    public dynamic CvDraft { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("template_id")]
    public string TemplateId { get; set; } = "default";

    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "pdf"; // latex | html | pdf

    [JsonPropertyName("target_role")]
    public string TargetRole { get; set; } = string.Empty;
}

public class RenderedCV
{
    [JsonPropertyName("cv_code")]
    public string CvCode { get; set; } = string.Empty; // Using string to handle both text and base64 encoded bytes

    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = string.Empty;

    [JsonPropertyName("sections")]
    public List<dynamic>? Sections { get; set; }
}

// Contact Agent
public class ContactInput
{
    [JsonPropertyName("optimized_cv")]
    public dynamic OptimizedCv { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("job_title")]
    public string JobTitle { get; set; } = string.Empty;

    [JsonPropertyName("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("job_description")]
    public string JobDescription { get; set; } = string.Empty;

    [JsonPropertyName("recipient_email")]
    public string RecipientEmail { get; set; } = string.Empty;

    [JsonPropertyName("cover_letter_hint")]
    public string? CoverLetterHint { get; set; }
}

public class ContactOutput
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("delivery_id")]
    public string DeliveryId { get; set; } = string.Empty;

    [JsonPropertyName("sent_at")]
    public DateTime SentAt { get; set; }

    [JsonPropertyName("subject_used")]
    public string SubjectUsed { get; set; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

// Orchestrator Feature Models
public class GenerateCvRequest
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("job_description")]
    public string JobDescription { get; set; } = string.Empty;

    [JsonPropertyName("candidate_name")]
    public string CandidateName { get; set; } = string.Empty;

    [JsonPropertyName("recipient_email")]
    public string RecipientEmail { get; set; } = string.Empty;
}
ParseOptions.0.jsonê
6/app/src/workflow-service/Models/WorkflowDefinition.cs¿namespace WorkflowService.Models;

public class WorkflowDefinition
{
    public List<WorkflowStep> Steps { get; set; } = new();
}

public class WorkflowStep
{
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
}
ParseOptions.0.jsonË
$/app/src/workflow-service/Program.cs™using Microsoft.EntityFrameworkCore;
using WorkflowService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WorkflowDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, o => o.UseVector());
});

builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddGrpc();

// Old untyped client & generic service
builder.Services.AddHttpClient();
builder.Services.AddScoped<WorkflowService.Services.WorkflowExecutionService>();

// New Strongly-Typed Agent SDK Clients
builder.Services.AddHttpClient<WorkflowService.AgentClients.IJobExtractorClient, WorkflowService.AgentClients.JobExtractorClient>(client =>
{
    client.BaseAddress = new Uri("http://cv-job-extractor:8001/api/v1/");
});

builder.Services.AddHttpClient<WorkflowService.AgentClients.ISearchAgentClient, WorkflowService.AgentClients.SearchAgentClient>(client =>
{
    client.BaseAddress = new Uri("http://cv-search-agent:8002/api/v1/");
});

builder.Services.AddHttpClient<WorkflowService.AgentClients.ITemplateAgentClient, WorkflowService.AgentClients.TemplateAgentClient>(client =>
{
    client.BaseAddress = new Uri("http://cv-template-agent:8003/api/v1/");
});

builder.Services.AddHttpClient<WorkflowService.AgentClients.IContactAgentClient, WorkflowService.AgentClients.ContactAgentClient>(client =>
{
    client.BaseAddress = new Uri("http://cv-contact-agent:8005/api/v1/");
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var pending = await dbContext.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            logger.LogInformation("Applying {Count} migrations...", pending.Count());
            await dbContext.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration failed");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapControllers();
app.MapGrpcService<WorkflowService.Grpc.WorkflowServiceImpl>();

app.Run();ParseOptions.0.jsonÒ
>/app/src/workflow-service/Services/WorkflowExecutionService.csôusing System.Text;
using System.Text.Json;
using WorkflowService.Models;

namespace WorkflowService.Services;

public class WorkflowExecutionService
{
    private readonly WorkflowService.AgentClients.IJobExtractorClient _jobExtractor;
    private readonly WorkflowService.AgentClients.ISearchAgentClient _searchAgent;
    private readonly WorkflowService.AgentClients.ICvOptimizerClient _cvOptimizer;
    private readonly WorkflowService.AgentClients.ITemplateAgentClient _templateAgent;
    private readonly WorkflowService.AgentClients.IContactAgentClient _contactAgent;
    private readonly ILogger<WorkflowExecutionService> _logger;

    public WorkflowExecutionService(
        WorkflowService.AgentClients.IJobExtractorClient jobExtractor,
        WorkflowService.AgentClients.ISearchAgentClient searchAgent,
        WorkflowService.AgentClients.ICvOptimizerClient cvOptimizer,
        WorkflowService.AgentClients.ITemplateAgentClient templateAgent,
        WorkflowService.AgentClients.IContactAgentClient contactAgent,
        ILogger<WorkflowExecutionService> logger)
    {
        _jobExtractor = jobExtractor;
        _searchAgent = searchAgent;
        _cvOptimizer = cvOptimizer;
        _templateAgent = templateAgent;
        _contactAgent = contactAgent;
        _logger = logger;
    }

    public async Task<ContactOutput?> GenerateTailoredCvAsync(GenerateCvRequest request)
    {
        _logger.LogInformation("Starting CV Generation Workflow for User {UserId}", request.UserId);

        // Step 1: Extract Job Requirements
        _logger.LogInformation("Step 1: Extracting job requirements...");
        var jobData = await _jobExtractor.ExtractAsync(new ExtractorInput { JobDescription = request.JobDescription });
        if (jobData == null) throw new Exception("Job extraction failed");

        // Step 2: Search User Profile for Matches
        _logger.LogInformation("Step 2: Searching for candidate matches...");
        var searchResult = await _searchAgent.MatchAsync(new SearchInput { UserId = request.UserId, JobRequirements = jobData });
        if (searchResult == null) throw new Exception("Profile search failed");

        // Step 3: Optimize CV (We pass the raw text as Optimizer expects)
        _logger.LogInformation("Step 3: Optimizing CV content...");
        var jobDataText = JsonSerializer.Serialize(jobData); // Formatted exactly as Optimizer expects
        var optimizedCv = await _cvOptimizer.OptimizeAsync(new OptimizerInput 
        { 
            JobData = jobDataText, 
            CandidateName = request.CandidateName,
            SessionId = Guid.NewGuid().ToString() // Generate a unique session
        });
        if (optimizedCv == null) throw new Exception("CV Optimization failed");

        // Step 4: Render Template
        _logger.LogInformation("Step 4: Rendering PDF Template...");
        var pdfResult = await _templateAgent.RenderAsync(new TemplateInput 
        { 
            CvDraft = new { }, // Pass necessary structure here
            TemplateType = "pdf",
            TargetRole = jobData.JobRole ?? "Professional"
        });
        if (pdfResult == null) throw new Exception("PDF Rendering failed");

        // Step 5: Deliver Email
        _logger.LogInformation("Step 5: Delivering via Contact Agent...");
        var deliveryResult = await _contactAgent.DeliverAsync(new ContactInput
        {
            OptimizedCv = new { FilePath = optimizedCv.FilePath }, // Match structure
            JobTitle = jobData.JobRole ?? "Job Opportunity",
            CompanyName = "Target Company", // Can extract this too
            JobDescription = request.JobDescription,
            RecipientEmail = request.RecipientEmail
        });

        _logger.LogInformation("Workflow Complete! Delivery Success: {Success}", deliveryResult?.Success);
        return deliveryResult;
    }
}
ParseOptions.0.json≥	
./app/src/workflow-service/WorkflowDbContext.csÎusing Microsoft.EntityFrameworkCore;
using WorkflowService.Entity;

namespace WorkflowService;

public class WorkflowDbContext : DbContext
{
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<AgentDocumentChunk> AgentDocumentChunks { get; set; }
    public DbSet<Experience> Experiences { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Skill> Skills { get; set; }

    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Register the pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // Automatically generate the tsvector for Full-Text Search based on the Content
        modelBuilder.Entity<AgentDocumentChunk>()
            .HasGeneratedTsVectorColumn(
                c => c.SearchVector,
                "english",
                c => new { c.Content })
            .HasIndex(c => c.SearchVector)
            .HasMethod("GIN");
    }
}ParseOptions.0.jsonÿ
M/app/src/workflow-service/obj/Debug/net10.0/WorkflowService.GlobalUsings.g.csÒ// <auto-generated/>
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Net.Http.Json;
global using System.Threading;
global using System.Threading.Tasks;
ParseOptions.0.jsonΩ
[/app/src/workflow-service/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs»// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
ParseOptions.0.json°
K/app/src/workflow-service/obj/Debug/net10.0/WorkflowService.AssemblyInfo.csº//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("WorkflowService")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0")]
[assembly: System.Reflection.AssemblyProductAttribute("WorkflowService")]
[assembly: System.Reflection.AssemblyTitleAttribute("WorkflowService")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json±
^/app/src/workflow-service/obj/Debug/net10.0/WorkflowService.MvcApplicationPartsAssemblyInfo.csπ//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute("Swashbuckle.AspNetCore.SwaggerGen")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.jsonÉ
C/app/src/workflow-service/obj/Debug/net10.0/EFCoreNpgsqlPgvector.cs¶//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: Microsoft.EntityFrameworkCore.Design.DesignTimeServicesReferenceAttribute(("Pgvector.EntityFrameworkCore.VectorDesignTimeServices, Pgvector.EntityFrameworkCo" +
    "re"), "Npgsql.EntityFrameworkCore.PostgreSQL")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json˝
Ω/app/src/workflow-service/obj/Debug/net10.0/Microsoft.AspNetCore.App.SourceGenerators/Microsoft.AspNetCore.SourceGenerators.PublicProgramSourceGenerator/PublicTopLevelProgram.Generated.g.cs•// <auto-generated />
/// <summary>
/// Auto-generated public partial Program class for top-level statement apps.
/// </summary>
public partial class Program { }ParseOptions.0.json