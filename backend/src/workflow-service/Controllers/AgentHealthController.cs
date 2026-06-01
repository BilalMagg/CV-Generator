using System.Diagnostics;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using WorkflowService.AgentClients;
using WorkflowService.Models;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/workflows/agents")]
public class AgentHealthController : ControllerBase
{
    private readonly IJobExtractorClient _jobExtractor;
    private readonly ISearchAgentClient _searchAgent;
    private readonly ITemplateAgentClient _templateAgent;
    private readonly ICvOptimizerClient _cvOptimizer;
    private readonly IContactAgentClient _contactAgent;
    private readonly IJobCrawlerClient _jobCrawler;
    private readonly ILogger<AgentHealthController> _logger;

    public AgentHealthController(
        IJobExtractorClient jobExtractor,
        ISearchAgentClient searchAgent,
        ITemplateAgentClient templateAgent,
        ICvOptimizerClient cvOptimizer,
        IContactAgentClient contactAgent,
        IJobCrawlerClient jobCrawler,
        ILogger<AgentHealthController> logger)
    {
        _jobExtractor = jobExtractor;
        _searchAgent = searchAgent;
        _templateAgent = templateAgent;
        _cvOptimizer = cvOptimizer;
        _contactAgent = contactAgent;
        _jobCrawler = jobCrawler;
        _logger = logger;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var healthTasks = new Dictionary<string, Func<Task<AgentHealthStatus>>>
        {
            ["jobExtractor"]  = () => CheckAgentHealthAsync("Job Extractor", _jobExtractor.CheckHealthAsync),
            ["searchAgent"]   = () => CheckAgentHealthAsync("Search Agent", _searchAgent.CheckHealthAsync),
            ["templateAgent"] = () => CheckAgentHealthAsync("Template Agent", _templateAgent.CheckHealthAsync),
            ["cvOptimizer"]   = () => CheckAgentHealthAsync("CV Optimizer", _cvOptimizer.CheckHealthAsync),
            ["contactAgent"]  = () => CheckAgentHealthAsync("Contact Agent", _contactAgent.CheckHealthAsync),
            ["jobCrawler"]    = () => CheckAgentHealthAsync("Job Crawler", _jobCrawler.CheckHealthAsync),
        };

        var tasks = healthTasks.ToDictionary(kv => kv.Key, kv => kv.Value());
        await Task.WhenAll(tasks.Values);

        var results = tasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);
        return Ok(ApiResponse<Dictionary<string, AgentHealthStatus>>.Ok(results));
    }

    private async Task<AgentHealthStatus> CheckAgentHealthAsync(string agentName, Func<Task<bool>> healthCheck)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var healthy = await healthCheck();
            sw.Stop();
            return new AgentHealthStatus
            {
                AgentName = agentName,
                Healthy = healthy,
                LatencyMs = sw.ElapsedMilliseconds,
                ErrorMessage = healthy ? null : "Health check returned unhealthy",
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Health check failed for {Agent}", agentName);
            return new AgentHealthStatus
            {
                AgentName = agentName,
                Healthy = false,
                LatencyMs = sw.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                LastChecked = DateTime.UtcNow
            };
        }
    }
}
