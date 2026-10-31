using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CV_Generator.Dto;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

/// <summary>Runtime deployment capabilities (what the sidecar can actually do),
/// used by the frontend to degrade gracefully — e.g. without a LaTeX engine the CV
/// pipeline still produces .tex sources while PDF buttons are disabled with a hint.</summary>
[ApiController]
[Authorize]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly ICapabilityService _capabilities;

    public SystemController(ICapabilityService capabilities)
    {
        _capabilities = capabilities;
    }

    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken ct)
        => Ok(ApiResponse<SystemCapabilitiesDto>.Ok(await _capabilities.GetAsync(ct)));
}
