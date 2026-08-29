using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Dto;
using CV_Generator.Models;
using CV_Generator.Services.AgentClients;
using CV_Generator.Data;

namespace CV_Generator.Services;

public class WorkflowExecutionService
{
    private readonly AppDbContext _db;
    private readonly IJobExtractorClient _jobExtractor;
    private readonly ISearchAgentClient _searchAgent;
    private readonly ICvOptimizerClient _cvOptimizer;
    private readonly ITemplateAgentClient _templateAgent;
    private readonly IContactAgentClient _contactAgent;
    private readonly IAgentLlmSettingsService _agentLlm;
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
        AppDbContext db,
        IJobExtractorClient jobExtractor,
        ISearchAgentClient searchAgent,
        ICvOptimizerClient cvOptimizer,
        ITemplateAgentClient templateAgent,
        IContactAgentClient contactAgent,
        IAgentLlmSettingsService agentLlm,
        ILogger<WorkflowExecutionService> logger)
    {
        _db = db;
        _jobExtractor = jobExtractor;
        _searchAgent = searchAgent;
        _cvOptimizer = cvOptimizer;
        _templateAgent = templateAgent;
        _contactAgent = contactAgent;
        _agentLlm = agentLlm;
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

        var extractorLlm = await _agentLlm.GetProviderModelAsync(run.UserId, "job-extractor", ct);
        var templateLlm = await _agentLlm.GetProviderModelAsync(run.UserId, "template-agent", ct);
        var optimizerLlm = await _agentLlm.GetProviderModelAsync(run.UserId, "cv-optimizer", ct);
        var contactLlm = await _agentLlm.GetProviderModelAsync(run.UserId, "contact-agent", ct);

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
                        Language = run.Language ?? "en",
                        Provider = extractorLlm.Provider,
                        Model = extractorLlm.Model,
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
                if (jobData != null && jobData.ExtractedSkills.Count == 0 && jobData.RequiredSkills.Count > 0)
                    jobData.ExtractedSkills = jobData.RequiredSkills;
                var targetRole = jobData?.JobRole ?? "Professional";

                var user = await _db.Users.FindAsync(new object[] { run.UserId }, ct);
                var locationParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(user?.City)) locationParts.Add(user.City);
                if (!string.IsNullOrWhiteSpace(user?.Country)) locationParts.Add(user.Country);

                var templateId = run.TemplateId ?? "default";
                var templateContent = await ResolveTemplateContentAsync(templateId, run.UserId, ct);

                var result = await _templateAgent.RenderAsync(new TemplateInput
                {
                    CvDraft = new Dictionary<string, object>
                    {
                        ["user_id"] = run.UserId,
                        ["target_role"] = targetRole,
                        ["summary"] = $"Professional summary for {run.CandidateName ?? user?.FirstName ?? "Candidate"}",
                        ["job_data"] = jobData != null ? JsonSerializer.Serialize(jobData) : "",
                        ["language"] = run.Language ?? "en",
                        ["tone"] = run.Tone ?? "professional",
                        ["profile"] = new Dictionary<string, object?>
                        {
                            ["name"] = $"{user?.FirstName} {user?.LastName}".Trim(),
                            ["headline"] = user?.Headline,
                            ["bio"] = user?.Bio,
                            ["location"] = string.Join(", ", locationParts),
                            ["email"] = user?.Email,
                            ["phone"] = user?.PhoneNumber
                        },
                        ["matched_skills"] = searchData?.MatchedSkills ?? new List<object>(),
                        ["matched_experiences"] = searchData?.MatchedExperiences ?? new List<object>(),
                        ["matched_projects"] = searchData?.MatchedProjects ?? new List<object>(),
                        ["gap_skills"] = searchData?.GapSkills ?? new List<string>()
                    },
                    TemplateId = templateId,
                    TemplateContent = templateContent,
                    TemplateType = "latex",
                    TargetRole = targetRole,
                    Provider = templateLlm.Provider,
                    Model = templateLlm.Model,
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
                    CvContent = rendered?.CvCode,
                    Provider = optimizerLlm.Provider,
                    Model = optimizerLlm.Model,
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
                    OptimizedCv = new Dictionary<string, object?>
                    {
                        ["job_id"] = runId,
                        ["final_sections"] = new List<object>(),
                        ["ats_score_estimate"] = optimizedCv?.AtsScoreAfter ?? 0,
                        ["optimization_notes"] = new List<string>(),
                        ["pdf_url"] = optimizedCv?.FilePath ?? "",
                        ["generated_at"] = DateTime.UtcNow
                    },
                    JobTitle = jobData?.JobRole ?? "Job Opportunity",
                    CompanyName = "Target Company",
                    JobDescription = run.JobDescription,
                    RecipientEmail = run.RecipientEmail ?? "",
                    CoverLetterHint = run.Tone != null ? $"Tone: {run.Tone}" : null,
                    Provider = contactLlm.Provider,
                    Model = contactLlm.Model,
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

    private async Task<string?> ResolveTemplateContentAsync(string templateId, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(templateId) || string.Equals(templateId, "default", StringComparison.OrdinalIgnoreCase))
            return null;

        // Custom/user templates are selected by Guid id; system templates carry the fixed seeded id.
        var template = Guid.TryParse(templateId, out var id)
            ? await _db.CvTemplates.FirstOrDefaultAsync(t => t.Id == id && (t.IsSystem || t.UserId == userId), ct)
            : await _db.CvTemplates.FirstOrDefaultAsync(t => t.Name == templateId && (t.IsSystem || t.UserId == userId), ct);

        return template?.Content;
    }
}
