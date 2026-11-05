using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[Route("api/gmail")]
public class GmailController : BaseApiController
{
    private readonly IGmailAuthService _gmailAuthSvc;
    private readonly string _frontendUrl;

    public GmailController(ICurrentUserService currentUser, IGmailAuthService gmailAuthSvc, IConfiguration config)
        : base(currentUser)
    {
        _gmailAuthSvc = gmailAuthSvc;
        _frontendUrl = config.GetValue<string>("Gmail:FrontendRedirectUrl") ?? "http://localhost:4200";
    }

    [HttpGet("connect")]
    public IActionResult Connect([FromQuery] string? returnUrl)
    {
        var userId = GetUserId();
        var url = _gmailAuthSvc.GetOAuthUrl(userId, EncodeReturnUrl(returnUrl));
        return Redirect(url);
    }

    // "userId" or "userId|{url-encoded returnUrl}" — keep the Guid prefix parseable by
    // HandleCallbackAsync and let the controller carry the page the user came from.
    private static string? EncodeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return null;
        var url = returnUrl.Trim();
        // Only same-origin relative paths — prevents using /api/gmail/connect as an open redirect.
        if (!url.StartsWith('/') || url.StartsWith("//")) return null;
        return url.StartsWith('/') ? url : $"/{url}";
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        // Split "userId|returnUrl" (the '|' part is optional and URL-encoded).
        var guidPart = state ?? string.Empty;
        var returnUrl = "/mailbox";
        var sep = guidPart.IndexOf('|');
        if (sep >= 0)
        {
            var decoded = Uri.UnescapeDataString(guidPart[(sep + 1)..]);
            if (decoded.StartsWith('/') && !decoded.StartsWith("//"))
                returnUrl = decoded;
            guidPart = guidPart[..sep];
        }

        await _gmailAuthSvc.HandleCallbackAsync(code, guidPart);
        return Redirect($"{_frontendUrl}{returnUrl}");
    }


    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userId = GetUserId();
        var status = await _gmailAuthSvc.GetStatusAsync(userId);
        if (status is null) return Ok(new { connected = false, needsReauth = false });
        return Ok(new
        {
            connected = !status.NeedsReauth,
            needsReauth = status.NeedsReauth,
            email = status.Email,
            connectedAt = status.ConnectedAt
        });
    }

    [HttpDelete("disconnect")]
    public async Task<IActionResult> Disconnect()
    {
        var userId = GetUserId();
        await _gmailAuthSvc.DisconnectAsync(userId);
        return NoContent();
    }
}
