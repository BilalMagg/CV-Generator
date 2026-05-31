using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/contacts")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactSvc;

    public ContactsController(IContactService contactSvc)
    {
        _contactSvc = contactSvc;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetAll(
        Guid userId,
        [FromQuery] string? search,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _contactSvc.GetContactsAsync(userId, search, source, page, pageSize);
        return Ok(ApiResponse<ContactListResponse>.Ok(result));
    }

    [HttpGet("{userId}/{id}")]
    public async Task<IActionResult> Get(Guid userId, Guid id)
    {
        var result = await _contactSvc.GetContactAsync(id, userId);
        if (result is null) return NotFound(ApiResponse<ContactDto>.Error("Contact not found"));
        return Ok(ApiResponse<ContactDto>.Ok(result));
    }

    [HttpPost("{userId}")]
    public async Task<IActionResult> Create(Guid userId, [FromBody] CreateContactDto dto)
    {
        var result = await _contactSvc.CreateContactAsync(userId, dto);
        return CreatedAtAction(nameof(Get), new { userId, id = result.Id }, ApiResponse<ContactDto>.Created(result));
    }

    [HttpPut("{userId}/{id}")]
    public async Task<IActionResult> Update(Guid userId, Guid id, [FromBody] UpdateContactDto dto)
    {
        var result = await _contactSvc.UpdateContactAsync(id, userId, dto);
        if (result is null) return NotFound(ApiResponse<ContactDto>.Error("Contact not found"));
        return Ok(ApiResponse<ContactDto>.Ok(result));
    }

    [HttpDelete("{userId}/{id}")]
    public async Task<IActionResult> Delete(Guid userId, Guid id)
    {
        var deleted = await _contactSvc.DeleteContactAsync(id, userId);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Contact not found"));
        return NoContent();
    }

    [HttpPost("import-csv")]
    public async Task<IActionResult> ImportCsv([FromBody] ImportCsvDto dto)
    {
        var count = await _contactSvc.ImportCsvAsync(dto.UserId, dto.CsvContent);
        return Ok(ApiResponse<object>.Ok(new { imported = count }));
    }

    [HttpPost("{userId}/import-from-offers")]
    public async Task<IActionResult> ImportFromOffers(Guid userId)
    {
        var count = await _contactSvc.ImportFromJobOffersAsync(userId);
        return Ok(ApiResponse<object>.Ok(new { imported = count }));
    }
}
