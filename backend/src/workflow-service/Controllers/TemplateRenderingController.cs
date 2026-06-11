using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService.AgentClients;
using WorkflowService.Entity;
using WorkflowService.Models;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/workflows/template")]
public class TemplateRenderingController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly ITemplateAgentClient _templateAgent;
    private readonly ILogger<TemplateRenderingController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public TemplateRenderingController(
        WorkflowDbContext db,
        ITemplateAgentClient templateAgent,
        ILogger<TemplateRenderingController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _templateAgent = templateAgent;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    // Render a CV by calling the template agent. The caller may supply cv_draft
    // directly (passthrough); otherwise it is built from the workflow DB for UserId.
    [HttpPost("render")]
    public async Task<IActionResult> Render([FromBody] TemplateRenderRequest request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Error("user_id is required"));
        if (string.IsNullOrWhiteSpace(request.TargetRole))
            return BadRequest(ApiResponse<object>.Error("target_role is required"));

        try
        {
            object cvDraft = request.CvDraft is { } supplied
                ? supplied
                : await BuildCvDraftFromDbAsync(request, ct);

            var result = await _templateAgent.RenderAsync(new TemplateInput
            {
                CvDraft = cvDraft,
                TemplateId = string.IsNullOrWhiteSpace(request.TemplateId) ? "default" : request.TemplateId,
                TargetRole = request.TargetRole,
                Tone = request.Tone
            }, ct);

            if (result == null)
                return StatusCode(502, ApiResponse<object>.Error("Template agent returned no result"));

            // Rewrite internal MinIO URLs so the browser can download through the API gateway
            if (!string.IsNullOrEmpty(result.PdfUrl))
            {
                var uri = new Uri(result.PdfUrl);
                if (uri.Host == "minio")
                    result.PdfUrl = $"/api/workflows/template/pdf?url={Uri.EscapeDataString(result.PdfUrl)}";
            }

            _logger.LogInformation(
                "Template render for user {UserId} (template {TemplateId}) completed: pdf={Pdf} code={Code}",
                request.UserId, request.TemplateId, result.PdfUrl, result.CodeUrl);

            return Ok(ApiResponse<RenderedCV>.Ok(result));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Template agent error");
            return StatusCode(502, ApiResponse<object>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Template render failed");
            return StatusCode(500, ApiResponse<object>.Error("Render failed: " + ex.Message));
        }
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
    {
        var templates = await _templateAgent.GetTemplatesAsync(ct);
        return Ok(ApiResponse<List<TemplateDefinition>>.Ok(templates));
    }

    [HttpGet("templates/{id}/preview")]
    public async Task<IActionResult> GetTemplatePreview(string id, CancellationToken ct)
    {
        var (data, contentType) = await _templateAgent.GetTemplatePreviewAsync(id, ct);
        if (data == null) return NotFound();
        return File(data, contentType);
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> ProxyPdf([FromQuery] string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("url is required");
        try
        {
            var http = _httpClientFactory.CreateClient();
            var bytes = await http.GetByteArrayAsync(url, ct);
            return File(bytes, "application/pdf", "cv.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF proxy failed for {Url}", url);
            return StatusCode(502, "Could not fetch PDF");
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var healthy = await _templateAgent.CheckHealthAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { healthy }));
    }

    // Build the agent cv_draft from the user's stored experiences/skills/projects.
    private async Task<Dictionary<string, object?>> BuildCvDraftFromDbAsync(
        TemplateRenderRequest request, CancellationToken ct)
    {
        var experiences = await _db.Experiences.Where(e => e.UserId == request.UserId).ToListAsync(ct);
        var skills = await _db.Skills.Where(s => s.UserId == request.UserId).ToListAsync(ct);
        var projects = await _db.Projects.Where(p => p.UserId == request.UserId).ToListAsync(ct);

        return new Dictionary<string, object?>
        {
            ["target_role"] = request.TargetRole,
            ["summary"] = string.IsNullOrWhiteSpace(request.Summary)
                ? $"Professional CV for {request.TargetRole}"
                : request.Summary,
            ["matched_skills"] = skills.Select(MapSkill).ToList(),
            ["matched_experiences"] = experiences.Select(MapExperience).ToList(),
            ["matched_projects"] = projects.Select(MapProject).ToList(),
            ["gap_skills"] = new List<string>()
        };
    }

    // Keys below match the agent's nested response-model field names (snake_case);
    // its models use populate_by_name=True so these are accepted directly.
    private static Dictionary<string, object?> MapSkill(Skill s) => new()
    {
        ["id"] = s.Id,
        ["name"] = s.Name,
        ["level"] = s.Level,
        ["category"] = s.Category,
        ["years_of_experience"] = s.YearsOfExperience,
        ["user_id"] = s.UserId
    };

    private static Dictionary<string, object?> MapExperience(Experience e) => new()
    {
        ["id"] = e.Id,
        ["title"] = e.Title,
        ["company"] = e.Company,
        ["description"] = e.Description,
        ["start_date"] = e.StartDate,
        ["end_date"] = e.EndDate,
        ["reference_url"] = e.ReferenceUrl,
        ["status"] = e.Status,
        ["user_id"] = e.UserId
    };

    private static Dictionary<string, object?> MapProject(Project p) => new()
    {
        ["id"] = p.Id,
        ["title"] = p.Title,
        ["description"] = p.Description,
        ["role"] = p.Role,
        ["achievements"] = p.Achievements,
        ["start_date"] = p.StartDate,
        ["end_date"] = p.EndDate,
        ["repository_url"] = p.RepositoryUrl,
        ["demo_url"] = p.DemoUrl,
        ["status"] = p.Status,
        ["user_id"] = p.UserId
    };
}
