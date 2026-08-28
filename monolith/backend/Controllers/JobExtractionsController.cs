using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;
using CV_Generator.Services;
using CV_Generator.Services.AgentClients;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/workflows/job-extractions")]
public class JobExtractionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJobExtractorClient _jobExtractor;
    private readonly ICurrentUserService _currentUser;
    private readonly IAgentLlmSettingsService _agentLlm;
    private readonly ILogger<JobExtractionsController> _logger;

    public JobExtractionsController(
        AppDbContext db,
        IJobExtractorClient jobExtractor,
        ICurrentUserService currentUser,
        IAgentLlmSettingsService agentLlm,
        ILogger<JobExtractionsController> logger)
    {
        _db = db;
        _jobExtractor = jobExtractor;
        _currentUser = currentUser;
        _agentLlm = agentLlm;
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
            var provider = "";
            var model = "";
            var userId = _currentUser.UserId;
            if (userId != null)
            {
                var llm = await _agentLlm.GetProviderModelAsync(userId, "job-extractor");
                provider = llm.Provider ?? "";
                model = llm.Model ?? "";
            }

            var result = await _jobExtractor.ExtractFullAsync(new JobExtractionRequest
            {
                Text = request.Text,
                Url = request.Url,
                JobOfferId = request.JobOfferId,
                Language = request.Language,
                Provider = request.Provider ?? (string.IsNullOrWhiteSpace(provider) ? null : provider),
                Model = request.Model ?? (string.IsNullOrWhiteSpace(model) ? null : model),
            });
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

    /// <summary>
    /// Saves an extraction's organized output to the library: upserts the Company
    /// (with its description) and creates a JobOffer so it can be referenced later.
    /// </summary>
    [HttpPost("{id:guid}/save-to-library")]
    public async Task<IActionResult> SaveToLibrary(Guid id)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Unauthorized();

        var entity = await _db.JobExtractions.FindAsync(id);
        if (entity is null)
            return NotFound(ApiResponse<object>.Error("Extraction not found"));

        ExtractionFullResult? result = null;
        try { result = JsonSerializer.Deserialize<ExtractionFullResult>(entity.OutputJson); }
        catch { /* fall back to entity scalar fields */ }

        result ??= new ExtractionFullResult
        {
            EnterpriseName = entity.CompanyName,
            JobRole = entity.JobRole,
            RawDescription = entity.InputSummary
        };

        var companyName = (result.EnterpriseName ?? entity.CompanyName).Trim();
        if (string.IsNullOrWhiteSpace(companyName))
            return BadRequest(ApiResponse<object>.Error("Extraction has no company name"));

        var company = await _db.Companies.FirstOrDefaultAsync(c =>
            c.UserId == userId && c.Name.ToLower() == companyName.ToLower());
        if (company is null)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Name = companyName,
                Description = result.EnterpriseDescription,
                Country = "Morocco",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Companies.Add(company);
        }
        else if (string.IsNullOrWhiteSpace(company.Description) && !string.IsNullOrWhiteSpace(result.EnterpriseDescription))
        {
            company.Description = result.EnterpriseDescription;
        }

        var jobOffer = new JobOffer
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            EnterpriseName = companyName,
            EnterpriseDescription = result.EnterpriseDescription,
            JobRole = result.JobRole ?? entity.JobRole,
            RawDescription = result.RawDescription ?? entity.InputSummary,
            RequiredExperienceYears = (int?)result.RequiredExperienceYears,
            SeniorityLevel = result.SeniorityLevel,
            EmploymentType = result.EmploymentType,
            Location = result.Location,
            LocationType = result.LocationType,
            EducationRequirements = result.EducationRequirements,
            SourceUrl = result.SourceUrl,
            Status = JobOfferStatus.OPEN,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.JobOffers.Add(jobOffer);

        await _db.SaveChangesAsync();

        _logger.LogInformation("Saved extraction {Id} to library: company {Company}, jobOffer {Job}", id, company.Id, jobOffer.Id);

        return Ok(ApiResponse<object>.Ok(new
        {
            companyId = company.Id,
            jobOfferId = jobOffer.Id,
            companyName = company.Name
        }));
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
