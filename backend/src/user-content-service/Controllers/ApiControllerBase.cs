using Microsoft.AspNetCore.Mvc;

namespace UserContentService.Controllers;

public class ApiControllerBase : ControllerBase
{
    protected Guid? GetUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var idStr) && Guid.TryParse(idStr?.ToString(), out var guid))
        {
            return guid;
        }
        return null;
    }

    protected Guid GetRequiredUserId()
    {
        var id = GetUserId();
        if (!id.HasValue)
        {
            // In production, this should probably throw an Unauthorized exception
            // but for now we'll return an empty Guid if really needed, 
            // though most logic should handle the null.
            return Guid.Empty;
        }
        return id.Value;
    }
}
