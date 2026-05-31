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

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid userId,
        [FromQuery] string? search,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _contactSvc.GetContactsAsync(userId, search, source, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid userId)
    {
        var result = await _contactSvc.GetContactAsync(id, userId);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromQuery] Guid userId, [FromBody] CreateContactDto dto)
    {
        var result = await _contactSvc.CreateContactAsync(userId, dto);
        return CreatedAtAction(nameof(Get), new { id = result.Id, userId }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromQuery] Guid userId, [FromBody] UpdateContactDto dto)
    {
        var result = await _contactSvc.UpdateContactAsync(id, userId, dto);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid userId)
    {
        var deleted = await _contactSvc.DeleteContactAsync(id, userId);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("import-csv")]
    public async Task<IActionResult> ImportCsv([FromBody] ImportCsvDto dto)
    {
        var count = await _contactSvc.ImportCsvAsync(dto.UserId, dto.CsvContent);
        return Ok(new { imported = count });
    }

    [HttpPost("import-from-offers")]
    public async Task<IActionResult> ImportFromOffers([FromQuery] Guid userId)
    {
        var count = await _contactSvc.ImportFromJobOffersAsync(userId);
        return Ok(new { imported = count });
    }
}
