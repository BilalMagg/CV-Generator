using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using CVGenerator.Shared;

namespace NotificationService.API.Controllers;

public class ContactsController : BaseApiController
{
    private readonly IContactService _contactSvc;

    public ContactsController(IContactService contactSvc)
    {
        _contactSvc = contactSvc;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var result = await _contactSvc.GetContactsAsync(userId, search, source, page, pageSize);
        return Ok(ApiResponse<ContactListResponse>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetUserId();
        var result = await _contactSvc.GetContactAsync(id, userId);
        if (result is null) return NotFound(ApiResponse<ContactDto>.Error("Contact not found"));
        return Ok(ApiResponse<ContactDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContactDto dto)
    {
        var userId = GetUserId();
        var result = await _contactSvc.CreateContactAsync(userId, dto);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse<ContactDto>.Created(result));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactDto dto)
    {
        var userId = GetUserId();
        var result = await _contactSvc.UpdateContactAsync(id, userId, dto);
        if (result is null) return NotFound(ApiResponse<ContactDto>.Error("Contact not found"));
        return Ok(ApiResponse<ContactDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var deleted = await _contactSvc.DeleteContactAsync(id, userId);
        if (!deleted) return NotFound(ApiResponse<object>.Error("Contact not found"));
        return NoContent();
    }

    [HttpPost("import-csv")]
    public async Task<IActionResult> ImportCsv([FromBody] ImportCsvDto dto)
    {
        var userId = GetUserId();
        var count = await _contactSvc.ImportCsvAsync(userId, dto.CsvContent);
        return Ok(ApiResponse<object>.Ok(new { imported = count }));
    }

    [HttpPost("import-from-offers")]
    public async Task<IActionResult> ImportFromOffers()
    {
        var userId = GetUserId();
        var count = await _contactSvc.ImportFromJobOffersAsync(userId);
        return Ok(ApiResponse<object>.Ok(new { imported = count }));
    }
}
