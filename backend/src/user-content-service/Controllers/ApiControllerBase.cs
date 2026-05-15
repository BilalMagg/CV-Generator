using Microsoft.AspNetCore.Mvc;

namespace UserContentService.Controllers;

public class ApiControllerBase : ControllerBase
{
    protected Guid? CurrentUserId => 
        Request.Headers.TryGetValue("X-User-Id", out var idStr) && Guid.TryParse(idStr.ToString(), out var guid) 
            ? guid 
            : null;

    protected Guid RequiredUserId => CurrentUserId ?? throw new UnauthorizedAccessException("User identification is missing.");
}
