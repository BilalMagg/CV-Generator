using System.Text;
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
