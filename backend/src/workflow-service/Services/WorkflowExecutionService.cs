using System.Text.Json;
using WorkflowService.AgentClients;
using WorkflowService.Entity;
using WorkflowService.Models;

namespace WorkflowService.Services;

public class WorkflowExecutionService
{
    private readonly WorkflowDbContext _db;
    private readonly IJobExtractorClient _jobExtractor;
    private readonly ISearchAgentClient _searchAgent;
    private readonly ICvOptimizerClient _cvOptimizer;
    private readonly ITemplateAgentClient _templateAgent;
    private readonly IContactAgentClient _contactAgent;
    private readonly ILogger<WorkflowExecutionService> _logger;

    private static readonly StepDefinition[] PipelineSteps =
    {
        new(0, "Job Extraction"),
        new(1, "Profile Matching"),
        new(2, "Template Rendering"),
        new(3, "CV Optimization"),
        new(4, "Email Delivery"),
    };

    public WorkflowExecutionService(
        WorkflowDbContext db,
        IJobExtractorClient jobExtractor,
        ISearchAgentClient searchAgent,
        ICvOptimizerClient cvOptimizer,
        ITemplateAgentClient templateAgent,
        IContactAgentClient contactAgent,
        ILogger<WorkflowExecutionService> logger)
    {
        _db = db;
        _jobExtractor = jobExtractor;
        _searchAgent = searchAgent;
        _cvOptimizer = cvOptimizer;
        _templateAgent = templateAgent;
        _contactAgent = contactAgent;
        _logger = logger;
    }

    public async Task ExecutePipelineAsync(Guid runId, CancellationToken ct)
    {
        var run = await _db.CvGenerationRuns.FindAsync(new object[] { runId }, ct);
        if (run == null)
        {
            _logger.LogWarning("Run {RunId} not found", runId);
            return;
        }

        run.Status = "running";
        run.CurrentStep = 0;
        run.StepStatuses = JsonSerializer.Serialize(PipelineSteps.Select(s => new StepStatusDto
        {
            Step = s.Index,
            Name = s.Name,
            Status = "pending"
        }).ToList());
        await _db.SaveChangesAsync(ct);

        try
        {
            ct.ThrowIfCancellationRequested();

            // Step 0: Job Extraction
            await ExecuteStepAsync(run, 0, ct, async () =>
            {
                var result = await _jobExtractor.ExtractAsync(
                    new ExtractorInput
                    {
                        JobDescription = run.JobDescription,
                        Language = run.Language ?? "en"
                    }, ct);
                run.ExtractionResult = JsonSerializer.Serialize(result);
            });

            ct.ThrowIfCancellationRequested();

            // Step 1: Profile Matching
            await ExecuteStepAsync(run, 1, ct, async () =>
            {
                var jobData = JsonSerializer.Deserialize<ExtractorOutput>(run.ExtractionResult ?? "{}");
                if (jobData != null)
                {
                    if (jobData.ExtractedSkills.Count == 0 && jobData.RequiredSkills.Count > 0)
                        jobData.ExtractedSkills = jobData.RequiredSkills;
                }
                var result = await _searchAgent.MatchAsync(
                    new SearchInput { UserId = run.UserId, JobRequirements = jobData ?? new() }, ct);
                run.SearchResult = JsonSerializer.Serialize(result);
            });

            ct.ThrowIfCancellationRequested();

            // Step 2: Template Rendering — generate first CV from matched profile
            await ExecuteStepAsync(run, 2, ct, async () =>
            {
                var searchData = JsonSerializer.Deserialize<SearchOutput>(run.SearchResult ?? "{}");
                var jobData = JsonSerializer.Deserialize<ExtractorOutput>(run.ExtractionResult ?? "{}");
                var result = await _templateAgent.RenderAsync(new TemplateInput
                {
                    CvDraft = new
                    {
                        matchedSkills = searchData?.MatchedSkills,
                        matchedExperiences = searchData?.MatchedExperiences,
                        matchedProjects = searchData?.MatchedProjects,
                        candidateName = run.CandidateName ?? "Candidate"
                    },
                    TemplateId = run.TemplateId ?? "default",
                    TemplateType = "pdf",
                    TargetRole = jobData?.JobRole ?? "Professional"
                }, ct);
                run.RenderResult = JsonSerializer.Serialize(result);
            });

            ct.ThrowIfCancellationRequested();

            // Step 3: CV Optimization — optimize the rendered CV
            await ExecuteStepAsync(run, 3, ct, async () =>
            {
                var rendered = JsonSerializer.Deserialize<RenderedCV>(run.RenderResult ?? "{}");
                var jobData = JsonSerializer.Deserialize<ExtractorOutput>(run.ExtractionResult ?? "{}");
                var jobDataText = JsonSerializer.Serialize(jobData);
                var result = await _cvOptimizer.OptimizeAsync(new OptimizerInput
                {
                    JobData = jobDataText,
                    CandidateName = run.CandidateName ?? "Candidate",
                    SessionId = Guid.NewGuid().ToString(),
                    CvContent = rendered?.CvCode
                }, ct);
                run.OptimizationResult = JsonSerializer.Serialize(result);
            });

            ct.ThrowIfCancellationRequested();

            // Step 4: Email Delivery
            await ExecuteStepAsync(run, 4, ct, async () =>
            {
                var optimizedCv = JsonSerializer.Deserialize<OptimizerOutput>(run.OptimizationResult ?? "{}");
                var jobData = JsonSerializer.Deserialize<ExtractorOutput>(run.ExtractionResult ?? "{}");
                var result = await _contactAgent.DeliverAsync(new ContactInput
                {
                    OptimizedCv = new { FilePath = optimizedCv?.FilePath ?? "" },
                    JobTitle = jobData?.JobRole ?? "Job Opportunity",
                    CompanyName = "Target Company",
                    JobDescription = run.JobDescription,
                    RecipientEmail = run.RecipientEmail ?? "",
                    CoverLetterHint = run.Tone != null ? $"Tone: {run.Tone}" : null
                }, ct);
                run.DeliveryResult = JsonSerializer.Serialize(result);
            });

            run.Status = "completed";
            run.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Run {RunId} completed successfully", runId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.Status = "cancelled";
            run.CancelledAt = DateTime.UtcNow;
            run.ErrorMessage = "Cancelled by user";
            _logger.LogInformation("Run {RunId} was cancelled via token", runId);
        }
        catch (Exception ex)
        {
            run.Status = "failed";
            run.ErrorMessage = ex.ToString();
            _logger.LogError(ex, "Run {RunId} failed", runId);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ExecuteStepAsync(CvGenerationRun run, int stepIndex, CancellationToken ct, Func<Task> action)
    {
        var stepName = PipelineSteps[stepIndex].Name;
        _logger.LogInformation("Step {Index}: {Name} — starting", stepIndex, stepName);

        var steps = JsonSerializer.Deserialize<List<StepStatusDto>>(run.StepStatuses ?? "[]")!;
        steps[stepIndex].Status = "running";
        steps[stepIndex].StartedAt = DateTime.UtcNow;
        run.StepStatuses = JsonSerializer.Serialize(steps);
        run.CurrentStep = stepIndex;
        await _db.SaveChangesAsync(ct);

        var startedAt = DateTime.UtcNow;

        try
        {
            await action();

            var duration = DateTime.UtcNow - startedAt;
            steps[stepIndex].Status = "completed";
            steps[stepIndex].CompletedAt = DateTime.UtcNow;
            steps[stepIndex].DurationMs = (long)duration.TotalMilliseconds;
            run.StepStatuses = JsonSerializer.Serialize(steps);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Step {Index}: {Name} — completed in {DurationMs}ms",
                stepIndex, stepName, steps[stepIndex].DurationMs);
        }
        catch (OperationCanceledException)
        {
            steps[stepIndex].Status = "cancelled";
            steps[stepIndex].Error = "Cancelled";
            run.StepStatuses = JsonSerializer.Serialize(steps);
            await _db.SaveChangesAsync(ct);
            throw;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startedAt;
            steps[stepIndex].Status = "failed";
            steps[stepIndex].CompletedAt = DateTime.UtcNow;
            steps[stepIndex].DurationMs = (long)duration.TotalMilliseconds;
            steps[stepIndex].Error = ex.Message;
            run.StepStatuses = JsonSerializer.Serialize(steps);
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    private record StepDefinition(int Index, string Name);
}
