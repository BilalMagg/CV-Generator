using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/gmail")]
public class GmailController : ControllerBase
{
    private readonly IGmailAuthService _gmailAuthSvc;

    public GmailController(IGmailAuthService gmailAuthSvc)
    {
        _gmailAuthSvc = gmailAuthSvc;
    }

    [HttpGet("connect")]
    public IActionResult Connect([FromQuery] Guid userId)
    {
        var url = _gmailAuthSvc.GetOAuthUrl(userId);
        return Redirect(url);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        await _gmailAuthSvc.HandleCallbackAsync(code, state);
        return Ok(new { message = "Gmail connected successfully" });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] Guid userId)
    {
        var status = await _gmailAuthSvc.GetStatusAsync(userId);
        if (status is null) return Ok(new { connected = false });
        return Ok(new { connected = true, email = status.Email, connectedAt = status.ConnectedAt });
    }

    [HttpDelete("disconnect")]
    public async Task<IActionResult> Disconnect([FromQuery] Guid userId)
    {
        await _gmailAuthSvc.DisconnectAsync(userId);
        return NoContent();
    }
}
