using Microsoft.AspNetCore.Mvc;
using CV_Generator.Services;
using CV_Generator.Services.AgentClients;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/workflows/crawl")]
public class CrawlController : ControllerBase
{
    private readonly IJobOfferServiceClient _client;
    private readonly ICurrentUserService _currentUser;

    public CrawlController(IJobOfferServiceClient client, ICurrentUserService currentUser)
    {
        _client = client;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> Trigger([FromBody] TriggerCrawlRequest request)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return BadRequest(ApiResponse<object>.Error("Unable to determine user identity"));

        request.UserId = userId.Value;
        var result = await _client.TriggerCrawlAsync(request);
        return Ok(ApiResponse<TriggerCrawlResponse>.Ok(result));
    }
}
