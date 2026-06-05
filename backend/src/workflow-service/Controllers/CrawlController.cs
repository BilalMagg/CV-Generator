using Microsoft.AspNetCore.Mvc;
using CVGenerator.Shared;
using WorkflowService.AgentClients;

namespace WorkflowService.Controllers;

[ApiController]
[Route("api/workflows/crawl")]
public class CrawlController : ControllerBase
{
    private readonly IJobOfferServiceClient _client;

    public CrawlController(IJobOfferServiceClient client) => _client = client;

    [HttpPost]
    public async Task<IActionResult> Trigger([FromBody] TriggerCrawlRequest request)
    {
        var result = await _client.TriggerCrawlAsync(request);
        return Ok(ApiResponse<TriggerCrawlResponse>.Ok(result));
    }
}
