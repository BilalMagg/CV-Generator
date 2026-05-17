using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using JobOfferService.DTOs;
using JobOfferService.Entities;
using JobOfferService.Hubs;
using JobOfferService.Repositories;
using JobOfferService.Services;
using JobOfferService.Validators;
using CVGenerator.Shared;
using FluentValidation;

namespace JobOfferService.Controllers;

[ApiController]
[Route("api/v1/job-offers")]
// [Authorize] // Uncomment this if the service requires JWT authentication
public class JobOffersController : ControllerBase
{
    private readonly IJobOfferService _service;
    private readonly IValidator<SubmitJobOfferDto> _submitValidator;
    private readonly IValidator<ExtractedJobDto> _extractedValidator;
    private readonly IValidator<UpdateJobStatusDto> _statusValidator;
    private readonly IHubContext<JobHub> _hub;
    private readonly ISearchCacheRepository _searchCacheRepo;
    private readonly IUserQuotaRepository _userQuotaRepo;
    private readonly JobOfferDbContext _db;
    private readonly IKafkaPublisher _kafka;
    private readonly ILogger<JobOffersController> _logger;

    public JobOffersController(
        IJobOfferService service,
        IValidator<SubmitJobOfferDto> submitValidator,
        IValidator<ExtractedJobDto> extractedValidator,
        IValidator<UpdateJobStatusDto> statusValidator,
        IHubContext<JobHub> hub,
        ISearchCacheRepository searchCacheRepo,
        IUserQuotaRepository userQuotaRepo,
        JobOfferDbContext db,
        IKafkaPublisher kafka,
        ILogger<JobOffersController> logger)
    {
        _service = service;
        _submitValidator = submitValidator;
        _extractedValidator = extractedValidator;
        _statusValidator = statusValidator;
        _hub = hub;
        _searchCacheRepo = searchCacheRepo;
        _userQuotaRepo = userQuotaRepo;
        _db = db;
        _kafka = kafka;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of job offers for a specific user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<JobOfferListDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // If you want to force the user ID from the token instead of query:
        // var currentUserId = Guid.Parse(GetUserId() ?? userId.ToString());

        var result = await _service.GetAllAsync(userId, page, pageSize);
        return Ok(ApiResponse<JobOfferListDto>.Ok(result));
    }

    /// <summary>
    /// Retrieves the full details of a specific job offer, including extracted skills.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<JobOfferDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var job = await _service.GetByIdAsync(id);
        if (job == null) 
        {
            _logger.LogWarning("Job offer with ID {Id} was not found.", id);
            return NotFound(ApiResponse<JobOfferDetailDto>.Error("Job offer not found"));
        }
        return Ok(ApiResponse<JobOfferDetailDto>.Ok(job));
    }

    /// <summary>
    /// Submits a raw job description or LinkedIn URL for AI processing.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> SubmitJob([FromBody] SubmitJobOfferDto dto)
    {
        var validation = await _submitValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Validation failed for SubmitJobOfferDto: {Errors}", validation.Errors);
            return BadRequest(ApiResponse<Guid>.Error(validation.Errors.First().ErrorMessage));
        }

        var createdId = await _service.SubmitRawJobOfferAsync(dto);
        _logger.LogInformation("Successfully submitted raw job offer with ID {Id}", createdId);
        
        return Created($"/api/v1/job-offers/{createdId}", ApiResponse<Guid>.Created(createdId));
    }

    /// <summary>
    /// Callback endpoint for the AI Agent to post the extracted JSON data.
    /// </summary>
    [HttpPost("{id:guid}/extracted")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> ProcessExtractedData(Guid id, [FromBody] ExtractedJobDto dto)
    {
        var validation = await _extractedValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<string>.Error(validation.Errors.First().ErrorMessage));
        }

        try
        {
            await _service.ProcessExtractedDataAsync(id, dto);
            _logger.LogInformation("Successfully processed AI extracted data for job offer {Id}", id);
            return Ok(ApiResponse<string>.Ok("Job offer data extracted and saved successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Failed to process extracted data. Job {Id} not found.", id);
            return NotFound(ApiResponse<string>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Updates the status of a job offer (e.g., DRAFT, OPEN, CLOSED).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateJobStatusDto dto)
    {
        var validation = await _statusValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<string>.Error(validation.Errors.First().ErrorMessage));
        }

        try
        {
            await _service.UpdateStatusAsync(id, dto);
            _logger.LogInformation("Updated status to {Status} for job offer {Id}", dto.Status, id);
            return Ok(ApiResponse<string>.Ok("Status updated successfully."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<string>.Error("Job offer not found"));
        }
    }

    /// <summary>
    /// Deletes a job offer entirely.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        // Added a check so you don't return 204 if it didn't exist
        var jobExists = await _service.GetByIdAsync(id);
        if (jobExists == null)
        {
             return NotFound(ApiResponse<object>.Error("Job offer not found"));
        }

        await _service.DeleteAsync(id);
        _logger.LogInformation("Deleted job offer {Id}", id);
        return NoContent();
    }

    /// <summary>
    /// Retrieves statistics for the user's dashboard.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<JobOfferStatisticsDto>), 200)]
    public async Task<IActionResult> GetStatistics([FromQuery] Guid userId)
    {
        if (userId == Guid.Empty) 
        {
            return BadRequest(ApiResponse<JobOfferStatisticsDto>.Error("userId query parameter is required."));
        }

        var stats = await _service.GetStatisticsAsync(userId);
        return Ok(ApiResponse<JobOfferStatisticsDto>.Ok(stats));
    }

    // ────────────────────────────────────────────────────────────────────
    // CRAWLER PIPELINE — POST /api/v1/job-offers/from-crawler
    // Called by the Python job-extractor using ExtractedJobDto (same DTO, same validator).
    // The 3 optional crawler fields (SearchId, Source, OverallConfidence) drive the
    // upsert logic and SignalR push; they are ignored by the normal /{id}/extracted flow.
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crawler ingestion endpoint: upsert-by-hash + SignalR push.
    /// Normal ingestion (SearchId == null) saves the job silently.
    /// </summary>
    [HttpPost("from-crawler")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 200)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> IngestFromCrawler([FromBody] ExtractedJobDto dto)
    {
        var validation = await _extractedValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<Guid>.Error(validation.Errors.First().ErrorMessage));

        // ── 1. Deduplication hash ───────────────────────────────────────────────
        var hash = ComputeJobHash(dto.JobRole, dto.EnterpriseName, dto.Location);

        // ── 2. Upsert ───────────────────────────────────────────────────────────
        var existing = await _db.JobOffers.FirstOrDefaultAsync(j => j.JobHash == hash);

        Guid jobId;
        JobOffer jobOffer;

        if (existing is not null)
        {
            existing.LastSeenAt = DateTime.UtcNow;
            existing.IsActive = true;
            await _db.SaveChangesAsync();
            jobId = existing.Id;
            jobOffer = existing;
            _logger.LogInformation("Upsert: refreshed job {Id} (hash={Hash})", jobId, hash);
        }
        else
        {
            jobOffer = new JobOffer
            {
                UserId = Guid.Empty,
                JobHash = hash,
                LastSeenAt = DateTime.UtcNow,
                IsActive = true,
                SourceUrl = dto.SourceUrl,
                RawDescription = dto.RawDescription,
                EnterpriseName = dto.EnterpriseName,
                EnterpriseDescription = dto.EnterpriseDescription,
                JobRole = dto.JobRole,
                Location = dto.Location,
                LocationType = dto.LocationType,
                EmploymentType = dto.EmploymentType,
                SeniorityLevel = dto.SeniorityLevel,
                RequiredExperienceYears = dto.RequiredExperienceYears,
                EducationRequirements = dto.EducationRequirements,
                Status = JobOfferStatus.OPEN,
            };

            foreach (var s in dto.RequiredSkills)
                jobOffer.Skills.Add(new JobSkill { Name = s, Type = SkillType.HARD_SKILL, IsMandatory = true });
            foreach (var s in dto.SoftSkills)
                jobOffer.Skills.Add(new JobSkill { Name = s, Type = SkillType.SOFT_SKILL, IsMandatory = false });
            foreach (var r in dto.Responsibilities)
                jobOffer.Responsibilities.Add(new JobResponsibility { Description = r });
            foreach (var b in dto.Benefits)
                jobOffer.Benefits.Add(new JobBenefit { Description = b });

            _db.JobOffers.Add(jobOffer);
            await _db.SaveChangesAsync();
            jobId = jobOffer.Id;
            _logger.LogInformation("Upsert: inserted new job {Id} (hash={Hash})", jobId, hash);
        }

        // ── 3. Batch Counting + SignalR (crawler pipeline only) ─────────────────
        if (dto.SearchId.HasValue)
        {
            var searchId = dto.SearchId.Value;

            await _searchCacheRepo.IncrementProcessedCountAsync(searchId);

            await _hub.Clients.Group(searchId.ToString()).SendAsync("JobArrived", new JobArrivedDto(
                JobId: jobId,
                Title: jobOffer.JobRole,
                Company: jobOffer.EnterpriseName,
                Location: jobOffer.Location,
                Source: dto.Source,
                JobUrl: jobOffer.SourceUrl,
                Confidence: dto.OverallConfidence
            ));

            var cache = await _searchCacheRepo.GetBySearchIdAsync(searchId);
            if (cache is not null && cache.ExpectedCount > 0 && cache.ProcessedCount >= cache.ExpectedCount)
            {
                cache.Status = SearchStatus.Completed;
                await _searchCacheRepo.UpdateAsync(cache);

                await _hub.Clients.Group(searchId.ToString()).SendAsync("SearchFinished",
                    new SearchFinishedDto(SearchId: searchId, TotalProcessed: cache.ProcessedCount));

                _logger.LogInformation(
                    "Search {SearchId} completed — {N} jobs processed.", searchId, cache.ProcessedCount);
            }

            return Ok(ApiResponse<Guid>.Ok(jobId));
        }

        return Created($"/api/v1/job-offers/{jobId}", ApiResponse<Guid>.Created(jobId));
    }

    // ─────────────────────────────────────────────────────────────────────
    // TRIGGER CRAWL  —  POST /api/v1/job-offers/crawl
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a live job crawl for the given keyword/location.
    ///
    /// Guards (in order):
    ///   1. Input validation — keyword and location required.
    ///   2. User quota      — one crawl per user per day (UTC).
    ///   3. Cache hit       — same keyword + location already crawled today:
    ///                        returns the existing SearchId (no new crawl).
    /// If all guards pass:
    ///   4. Creates a SearchCache row (Status=Pending).
    ///   5. Publishes trigger to 'trigger-live-crawl' Kafka topic.
    ///   6. Marks quota used for the user.
    ///   7. Returns SearchId — frontend joins the SignalR group with this.
    /// </summary>
    [HttpPost("crawl")]
    [ProducesResponseType(typeof(ApiResponse<TriggerCrawlResponseDto>), 200)]   // cache hit
    [ProducesResponseType(typeof(ApiResponse<TriggerCrawlResponseDto>), 202)]   // new crawl
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 429)]
    public async Task<IActionResult> TriggerCrawl([FromBody] TriggerCrawlDto dto)
    {
        // ── Guard 1: Input validation ────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(dto.Keyword))
            return BadRequest(ApiResponse<object>.Error("Keyword is required."));
        if (string.IsNullOrWhiteSpace(dto.Location))
            return BadRequest(ApiResponse<object>.Error("Location is required."));

        // ── Guard 2: Daily user quota ───────────────────────────────────────────────
        if (await _userQuotaRepo.HasCrawledTodayAsync(dto.UserId))
        {
            _logger.LogWarning("Quota exceeded | UserId={UserId}", dto.UserId);
            return StatusCode(429, ApiResponse<object>.Error(
                "Daily crawl limit reached. You can trigger one crawl per day."));
        }

        // ── Guard 3: Cache hit — same search already done today ────────────────
        var existing = await _searchCacheRepo.GetTodayCacheAsync(dto.Keyword, dto.Location);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Cache hit | SearchId={SearchId} Keyword={Keyword} Location={Location} Status={Status}",
                existing.SearchId, existing.Keyword, existing.Location, existing.Status);

            return Ok(ApiResponse<TriggerCrawlResponseDto>.Ok(new TriggerCrawlResponseDto(
                SearchId: existing.SearchId,
                Keyword: existing.Keyword,
                Location: existing.Location ?? dto.Location,
                ResultLimit: dto.ResultLimit
            )));
        }

        // ── All guards passed — start a new crawl ────────────────────────────────
        var searchId = Guid.NewGuid();

        // 4. Pre-create SearchCache so the Kafka summary consumer can update it
        await _searchCacheRepo.CreateAsync(new JobOfferService.Entities.SearchCache
        {
            SearchId = searchId,
            Keyword  = dto.Keyword,
            Location = dto.Location,
            Status   = JobOfferService.Entities.SearchStatus.Pending,
        });

        // 5. Fire the Kafka trigger
        await _kafka.PublishAsync("trigger-live-crawl", new
        {
            search_id    = searchId,
            keyword      = dto.Keyword,
            location     = dto.Location,
            result_limit = dto.ResultLimit,
        });

        // 6. Mark quota used for today
        await _userQuotaRepo.UpsertAsync(dto.UserId);

        _logger.LogInformation(
            "Crawl triggered | SearchId={SearchId} Keyword={Keyword} Location={Location} Limit={Limit}",
            searchId, dto.Keyword, dto.Location, dto.ResultLimit);

        // 7. Return SearchId — frontend joins the SignalR group with this
        return Accepted(ApiResponse<TriggerCrawlResponseDto>.Ok(new TriggerCrawlResponseDto(
            SearchId: searchId,
            Keyword: dto.Keyword,
            Location: dto.Location,
            ResultLimit: dto.ResultLimit
        )));
    }

    // ── Job Hash Generator ────────────────────────────────────────────────────
    private static string ComputeJobHash(string? role, string? company, string? location)
    {
        var raw = $"{role}_{company}_{location}"
            .ToLowerInvariant()
            .Replace(" ", "");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..32];
    }
}