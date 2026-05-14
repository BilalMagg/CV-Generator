using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;
using UserContentService.Events.CertificationEvent;
using UserContentService.Services;
using UserContentService.dto.Certification;

namespace UserContentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CertificationsController : ApiControllerBase
{
    private readonly ContentDbContext _db;
    private readonly KafkaProducerService _kafkaProducer;

    public CertificationsController(ContentDbContext db, KafkaProducerService kafkaProducer)
    {
        _db = db;
        _kafkaProducer = kafkaProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        userId ??= GetUserId();

        var certs = userId.HasValue
            ? await _db.Certifications.Where(c => c.UserId == userId.Value).ToListAsync()
            : await _db.Certifications.ToListAsync();

        var response = certs.Select(c => new CertificationResponseDto
        {
            Id = c.Id,
            Name = c.Name,
            IssuingOrganization = c.IssuingOrganization,
            IssueDate = c.IssueDate,
            CredentialUrl = c.CredentialUrl,
            UserId = c.UserId
        }).ToList();

        return Ok(ApiResponse<List<CertificationResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await _db.Certifications.FindAsync(id);
        if (c == null) return NotFound(ApiResponse<CertificationResponseDto>.Error("Certification not found"));

        var response = new CertificationResponseDto
        {
            Id = c.Id,
            Name = c.Name,
            IssuingOrganization = c.IssuingOrganization,
            IssueDate = c.IssueDate,
            CredentialUrl = c.CredentialUrl,
            UserId = c.UserId
        };
        return Ok(ApiResponse<CertificationResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCertificationDto dto)
    {
        if (dto.UserId == Guid.Empty) dto.UserId = GetRequiredUserId();

        var cert = new Certification
        {
            Name = dto.Name,
            IssuingOrganization = dto.IssuingOrganization,
            IssueDate = dto.IssueDate,
            CredentialUrl = dto.CredentialUrl,
            UserId = dto.UserId
        };

        _db.Certifications.Add(cert);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("certification-created", new CertificationCreatedEvent
        {
            Id = cert.Id,
            Name = cert.Name,
            IssuingOrganization = cert.IssuingOrganization,
            IssueDate = cert.IssueDate,
            CredentialUrl = cert.CredentialUrl,
            UserId = cert.UserId
        });

        var response = new CertificationResponseDto
        {
            Id = cert.Id,
            Name = cert.Name,
            IssuingOrganization = cert.IssuingOrganization,
            IssueDate = cert.IssueDate,
            CredentialUrl = cert.CredentialUrl,
            UserId = cert.UserId
        };
        return CreatedAtAction(nameof(GetById), new { id = cert.Id }, ApiResponse<CertificationResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCertificationDto dto)
    {
        var cert = await _db.Certifications.FindAsync(id);
        if (cert == null) return NotFound(ApiResponse<CertificationResponseDto>.Error("Certification not found"));

        cert.Name = dto.Name;
        cert.IssuingOrganization = dto.IssuingOrganization;
        cert.IssueDate = dto.IssueDate;
        cert.CredentialUrl = dto.CredentialUrl;

        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("certification-updated", new CertificationUpdatedEvent
        {
            Id = cert.Id,
            Name = cert.Name,
            IssuingOrganization = cert.IssuingOrganization,
            IssueDate = cert.IssueDate,
            CredentialUrl = cert.CredentialUrl,
            UserId = cert.UserId
        });

        var response = new CertificationResponseDto
        {
            Id = cert.Id,
            Name = cert.Name,
            IssuingOrganization = cert.IssuingOrganization,
            IssueDate = cert.IssueDate,
            CredentialUrl = cert.CredentialUrl,
            UserId = cert.UserId
        };
        return Ok(ApiResponse<CertificationResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cert = await _db.Certifications.FindAsync(id);
        if (cert == null) return NotFound(ApiResponse<object>.Error("Certification not found"));

        var userId = cert.UserId;

        _db.Certifications.Remove(cert);
        await _db.SaveChangesAsync();

        await _kafkaProducer.PublishAsync("certification-deleted", new CertificationDeletedEvent
        {
            Id = id,
            UserId = userId
        });

        return NoContent();
    }

}
