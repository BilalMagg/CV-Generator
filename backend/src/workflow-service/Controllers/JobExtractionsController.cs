using System.Text.Json;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkflowService.AgentClients;
using WorkflowService.Entity;
using WorkflowService.Models;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/workflows/job-extractions")]
public class JobExtractionsController : ControllerBase
{
    private readonly WorkflowDbContext _db;
    private readonly IJobExtractorClient _jobExtractor;
    private readonly ILogger<JobExtractionsController> _logger;

    public JobExtractionsController(
        WorkflowDbContext db,
        IJobExtractorClient jobExtractor,
        ILogger<JobExtractionsController> logger)
    {
        _db = db;
        _jobExtractor = jobExtractor;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Extract([FromBody] JobExtractionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text) &&
            string.IsNullOrWhiteSpace(request.Url) &&
            string.IsNullOrWhiteSpace(request.JobOfferId))
        {
            return BadRequest(ApiResponse<object>.Error("Provide text, URL, or job offer ID"));
        }

        try
        {
            var result = await _jobExtractor.ExtractFullAsync(request);
            if (result == null)
                return StatusCode(502, ApiResponse<object>.Error("Job extractor agent returned no result"));

            var entity = new JobExtractionEntity
            {
                InputType = GetInputType(request),
                InputSummary = GetInputSummary(request),
                OutputJson = JsonSerializer.Serialize(result),
                JobRole = result.JobRole ?? "Unknown",
                CompanyName = result.EnterpriseName ?? "Unknown",
                Confidence = result.OverallConfidence,
                CreatedAt = DateTime.UtcNow
            };

            _db.JobExtractions.Add(entity);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Job extraction {Id} completed: {Role} at {Company}",
                entity.Id, entity.JobRole, entity.CompanyName);

            return Ok(ApiResponse<ExtractionResponse>.Ok(new ExtractionResponse
            {
                Id = entity.Id,
                Output = result
            }));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Job extractor agent error");
            return StatusCode(502, ApiResponse<object>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job extraction failed");
            return StatusCode(500, ApiResponse<object>.Error("Extraction failed: " + ex.Message));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var entity = await _db.JobExtractions.FindAsync(id);
        if (entity == null)
            return NotFound(ApiResponse<object>.Error("Extraction not found"));

        var output = JsonSerializer.Deserialize<ExtractionFullResult>(entity.OutputJson);
        return Ok(ApiResponse<ExtractionResponse>.Ok(new ExtractionResponse
        {
            Id = entity.Id,
            Output = output ?? new ExtractionFullResult()
        }));
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int limit = 20)
    {
        var items = await _db.JobExtractions
            .OrderByDescending(e => e.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(e => new ExtractionHistoryItemDto
            {
                Id = e.Id,
                JobRole = e.JobRole,
                CompanyName = e.CompanyName,
                CreatedAt = e.CreatedAt,
                OverallConfidence = e.Confidence,
                SourceType = e.InputType
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ExtractionHistoryItemDto>>.Ok(items));
    }

    private static string GetInputType(JobExtractionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Text)) return "text";
        if (!string.IsNullOrWhiteSpace(request.Url)) return "url";
        if (!string.IsNullOrWhiteSpace(request.JobOfferId)) return "offer";
        return "unknown";
    }

    private static string GetInputSummary(JobExtractionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Text))
            return request.Text.Length > 200 ? request.Text[..200] + "..." : request.Text;
        if (!string.IsNullOrWhiteSpace(request.Url)) return request.Url;
        if (!string.IsNullOrWhiteSpace(request.JobOfferId)) return $"Job Offer #{request.JobOfferId}";
        return "";
    }
}
