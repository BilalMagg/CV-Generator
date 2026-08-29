using Microsoft.AspNetCore.Mvc;
using CV_Generator.Dto;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/llm-settings")]
public class LlmSettingsController : ControllerBase
{
    private readonly ILlmSettingsService _llmSettingsService;
    private readonly IAgentLlmSettingsService _agentLlmSettingsService;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpClientFactory _httpClientFactory;

    public LlmSettingsController(
        ILlmSettingsService llmSettingsService,
        IAgentLlmSettingsService agentLlmSettingsService,
        ICurrentUserService currentUser,
        IHttpClientFactory httpClientFactory)
    {
        _llmSettingsService = llmSettingsService;
        _agentLlmSettingsService = agentLlmSettingsService;
        _currentUser = currentUser;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Unauthorized(new { success = false, message = "User not authenticated" });

        var settings = await _llmSettingsService.GetAsync(userId);
        return Ok(new { success = true, data = settings });
    }

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] LlmSettingsRequest request)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Unauthorized(new { success = false, message = "User not authenticated" });

        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.Model))
            return BadRequest(new { success = false, message = "Provider and model are required" });

        var settings = await _llmSettingsService.SetAsync(userId, request.Provider, request.Model);
        return Ok(new { success = true, data = settings });
    }

    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders()
    {
        var http = _httpClientFactory.CreateClient("agents");
        var resp = await http.GetAsync("/api/llm/providers");
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { success = false, message = "LLM catalog unavailable" });
        var body = await resp.Content.ReadFromJsonAsync<LlmProvidersResponseDto>();
        return Ok(new { success = true, data = body });
    }

    [HttpGet("models")]
    public async Task<IActionResult> GetModels([FromQuery] string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BadRequest(new { success = false, message = "provider query param required" });
        var http = _httpClientFactory.CreateClient("agents");
        var resp = await http.GetAsync($"/api/llm/models?provider={Uri.EscapeDataString(provider)}");
        if (!resp.IsSuccessStatusCode)
            return StatusCode(502, new { success = false, message = "LLM catalog unavailable" });
        var body = await resp.Content.ReadFromJsonAsync<LlmModelsResponseDto>();
        return Ok(new { success = true, data = body });
    }

    // ── Per-agent LLM configuration ──────────────────────────────────────────

    [HttpGet("agents")]
    public async Task<IActionResult> GetAgentSettings()
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Unauthorized(new { success = false, message = "User not authenticated" });

        var settings = await _agentLlmSettingsService.GetAllAsync(userId);
        return Ok(new { success = true, data = settings });
    }

    [HttpPut("agents/{agentId}")]
    public async Task<IActionResult> SetAgentSetting(string agentId, [FromBody] AgentLlmSettingsRequest request)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return Unauthorized(new { success = false, message = "User not authenticated" });

        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.Model))
            return BadRequest(new { success = false, message = "Provider and model are required" });

        try
        {
            var settings = await _agentLlmSettingsService.SetAsync(userId, agentId, request.Provider, request.Model);
            return Ok(new { success = true, data = settings });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = $"Unknown agent '{agentId}'" });
        }
    }
}
