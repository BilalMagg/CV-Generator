using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NotificationService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected Guid GetUserId()
    {
        var userId = User.FindFirstValue("user_id");
        return Guid.Parse(userId!);
    }
}
