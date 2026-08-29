using CV_Generator.Dto;
using CV_Generator.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _service;
    private readonly ICurrentUserService _currentUser;

    public CategoriesController(ICategoryService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid? UserId => _currentUser.UserId;

    /// GET /api/categories/tree?scope=projects
    [HttpGet("tree")]
    public async Task<IActionResult> GetTree([FromQuery] string scope)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        if (string.IsNullOrWhiteSpace(scope)) return BadRequest(ApiResponse<object>.Error("scope is required"));
        var tree = await _service.GetTreeAsync(UserId.Value, scope);
        return Ok(ApiResponse<List<CategoryNodeDto>>.Ok(tree));
    }

    /// POST /api/categories/search
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] CategorySearchRequest request)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        var results = await _service.SearchAsync(UserId.Value, request.NodeIds, request.SourceTypes);
        return Ok(ApiResponse<List<CategorySearchResult>>.Ok(results));
    }

    /// GET /api/categories/tags?sourceType=projects&sourceId=<guid>
    [HttpGet("tags")]
    public async Task<IActionResult> GetTags([FromQuery] string sourceType, [FromQuery] string sourceId)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceId))
            return BadRequest(ApiResponse<object>.Error("sourceType and sourceId are required"));
        if (!Guid.TryParse(sourceId, out var id))
            return BadRequest(ApiResponse<object>.Error("sourceId must be a valid GUID"));
        var tagIds = await _service.GetTagsAsync(UserId.Value, sourceType, id);
        return Ok(ApiResponse<List<Guid>>.Ok(tagIds));
    }

    /// PUT /api/categories/tags
    [HttpPut("tags")]
    public async Task<IActionResult> SetTags([FromBody] CategoryTagRequest request)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));
        if (string.IsNullOrWhiteSpace(request.SourceType)) return BadRequest(ApiResponse<object>.Error("sourceType is required"));
        await _service.SetTagsAsync(UserId.Value, request.SourceType, request.SourceId, request.NodeIds);
        return Ok(ApiResponse<object>.Ok(null));
    }
}
