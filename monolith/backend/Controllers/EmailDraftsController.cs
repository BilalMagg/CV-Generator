using Microsoft.AspNetCore.Mvc;
using CV_Generator;
using CV_Generator.Dto;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

/// <summary>Reusable application-email drafts saved from the Apply wizard.</summary>
[Route("api/email-drafts")]
public class EmailDraftsController : BaseApiController
{
    private readonly EmailDraftService _svc;
    private readonly IMinioStorageService _minio;

    public EmailDraftsController(ICurrentUserService currentUser, EmailDraftService svc, IMinioStorageService minio)
        : base(currentUser)
    {
        _svc = svc;
        _minio = minio;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        var result = await _svc.ListAsync(userId);
        return Ok(ApiResponse<List<EmailDraftListItemDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetUserId();
        var result = await _svc.GetAsync(id, userId);
        if (result is null) return NotFound(ApiResponse<EmailDraftDto>.Error("Draft not found"));
        return Ok(ApiResponse<EmailDraftDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmailDraftDto dto)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(ApiResponse<EmailDraftDto>.Error("Name is required"));
        var result = await _svc.CreateAsync(userId, dto);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse<EmailDraftDto>.Created(result));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmailDraftDto dto)
    {
        var userId = GetUserId();
        var result = await _svc.UpdateAsync(id, userId, dto);
        if (result is null) return NotFound(ApiResponse<EmailDraftDto>.Error("Draft not found"));
        return Ok(ApiResponse<EmailDraftDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var deleted = await _svc.DeleteAsync(id, userId);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Draft not found"));
        return NoContent();
    }

    /// GET /email-drafts/{id}/attachments/{index}/download — streams an attachment's bytes
    [HttpGet("{id}/attachments/{index}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid id, int index)
    {
        var userId = GetUserId();
        var att = await _svc.GetAttachmentAsync(id, index, userId);
        if (att == null || !att.ObjectKey.StartsWith($"{userId}/", StringComparison.Ordinal))
            return NotFound(ApiResponse<object>.Error("Attachment not found"));

        // Display name: objectKey = "{userId}/{guid}_{originalName}" → strip "{guid}_" prefix.
        var keyName = att.ObjectKey[(att.ObjectKey.LastIndexOf('/') + 1)..];
        string displayName;
        var underscore = keyName.IndexOf('_');
        displayName = underscore > 0 && underscore < keyName.Length - 1 ? keyName[(underscore + 1)..] : keyName;

        try
        {
            var bytes = await _minio.GetObjectAsync("mail-attachments", att.ObjectKey);
            if (bytes.Length == 0) return NotFound(ApiResponse<object>.Error("Attachment not found"));
            return File(bytes, "application/octet-stream", displayName);
        }
        catch
        {
            return NotFound(ApiResponse<object>.Error("Attachment not found"));
        }
    }
}