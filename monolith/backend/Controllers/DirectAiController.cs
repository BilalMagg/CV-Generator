using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CV_Generator.Dto;
using CV_Generator.Services;
using CV_Generator.Services.AgentClients;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/direct-ai")]
[Authorize]
public class DirectAiController : ControllerBase
{
    private readonly IDirectAiClient _client;
    private readonly ICurrentUserService _currentUser;
    private readonly IAgentLlmSettingsService _agentLlm;

    public DirectAiController(
        IDirectAiClient client,
        ICurrentUserService currentUser,
        IAgentLlmSettingsService agentLlm)
    {
        _client = client;
        _currentUser = currentUser;
        _agentLlm = agentLlm;
    }

    /// <summary>Generate a short outreach / application message (direct LLM call, OpenRouter-first).</summary>
    [HttpPost("message")]
    public async Task<IActionResult> Message([FromBody] DirectMessageRequestDto request)
    {
        await ResolveProviderAsync(request);
        if (string.IsNullOrWhiteSpace(request.CompanyName) && string.IsNullOrWhiteSpace(request.JobRole))
            return BadRequest(ApiResponse<object>.Error("Provide at least a company name or job role to compose a message around"));

        var result = await _client.GenerateMessageAsync(request);
        if (result == null)
            return StatusCode(502, ApiResponse<object>.Error("AI assistant could not be reached"));

        return Ok(ApiResponse<DirectMessageResultDto>.Ok(result));
    }

    /// <summary>Generic direct chat call (OpenRouter-first). Used for other direct AI functionality.</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] DirectChatRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.User))
            return BadRequest(ApiResponse<object>.Error("Provide a prompt"));

        await ResolveProviderAsync(request);
        var result = await _client.ChatAsync(request);
        if (result == null)
            return StatusCode(502, ApiResponse<object>.Error("AI assistant could not be reached"));

        return Ok(ApiResponse<DirectChatResultDto>.Ok(result));
    }

    private async Task ResolveProviderAsync(object request)
    {
        var userId = _currentUser.UserId;
        string provider = "", model = "";
        if (userId != null)
        {
            var llm = await _agentLlm.GetProviderModelAsync(userId, "direct");
            provider = llm.Provider ?? "";
            model = llm.Model ?? "";
        }

        // Only override when the caller didn't already pin provider/model, and a
        // saved per-user override exists. Otherwise let the sidecar default to
        // OpenRouter-first via its own provider priority.
        switch (request)
        {
            case DirectMessageRequestDto m:
                m.Provider ??= string.IsNullOrWhiteSpace(provider) ? null : provider;
                m.Model ??= string.IsNullOrWhiteSpace(model) ? null : model;
                break;
            case DirectChatRequestDto c:
                c.Provider ??= string.IsNullOrWhiteSpace(provider) ? null : provider;
                c.Model ??= string.IsNullOrWhiteSpace(model) ? null : model;
                break;
        }
    }
}
