using CV_Generator.Services;
using Microsoft.AspNetCore.Mvc;

namespace CV_Generator.Controllers;

public class ApiControllerBase : ControllerBase
{
    protected Guid? CurrentUserId =>
        HttpContext.RequestServices.GetService<ICurrentUserService>()?.UserId;

    protected Guid RequiredUserId => CurrentUserId ?? throw new UnauthorizedAccessException("User identification is missing.");
}
