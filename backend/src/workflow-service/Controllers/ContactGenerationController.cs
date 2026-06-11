using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using WorkflowService.AgentClients;
using WorkflowService.Models;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/workflows/contact-generate")]
public class ContactGenerationController : ControllerBase
{
    private readonly IContactAgentClient _contactAgent;
    private readonly ILogger<ContactGenerationController> _logger;

    public ContactGenerationController(
        IContactAgentClient contactAgent,
        ILogger<ContactGenerationController> logger)
    {
        _contactAgent = contactAgent;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GenerateEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.JobTitle) ||
            string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.JobDescription))
        {
            return BadRequest(ApiResponse<object>.Error("job_title, company_name, and job_description are required"));
        }

        try
        {
            var result = await _contactAgent.GenerateAsync(request);
            if (result == null)
                return StatusCode(502, ApiResponse<object>.Error("Contact agent returned no result"));

            return Ok(ApiResponse<GenerateEmailResponse>.Ok(result));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Contact agent error during email generation");
            return StatusCode(502, ApiResponse<object>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email generation failed");
            return StatusCode(500, ApiResponse<object>.Error("Generation failed: " + ex.Message));
        }
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> GenerateBulk([FromBody] BulkGenerateContactRequest request)
    {
        if (request.Contacts == null || request.Contacts.Count == 0)
            return BadRequest(ApiResponse<object>.Error("At least one contact is required"));

        if (string.IsNullOrWhiteSpace(request.JobTitle) ||
            string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.JobDescription))
        {
            return BadRequest(ApiResponse<object>.Error("jobTitle, companyName, and jobDescription are required"));
        }

        try
        {
            var result = await _contactAgent.GenerateBulkAsync(request);
            return Ok(ApiResponse<BulkGenerateContactResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk email generation failed");
            return StatusCode(500, ApiResponse<object>.Error("Bulk generation failed: " + ex.Message));
        }
    }
}
