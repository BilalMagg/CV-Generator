using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobOfferService.DTOs;
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
    private readonly ILogger<JobOffersController> _logger;

    public JobOffersController(
        IJobOfferService service,
        IValidator<SubmitJobOfferDto> submitValidator,
        IValidator<ExtractedJobDto> extractedValidator,
        IValidator<UpdateJobStatusDto> statusValidator,
        ILogger<JobOffersController> logger)
    {
        _service = service;
        _submitValidator = submitValidator;
        _extractedValidator = extractedValidator;
        _statusValidator = statusValidator;
        _logger = logger;
    }

    private string? GetUserId()
    {
        // Extracts the User ID from the JWT Token
        return User.FindFirst("sub")?.Value 
            ?? User.FindFirst("local_user_id")?.Value;
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
}