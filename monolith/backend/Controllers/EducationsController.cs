using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator;
using CV_Generator.Models;
using CV_Generator.Data;
using CV_Generator.Dto;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EducationsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<EducationsController> _logger;

    public EducationsController(AppDbContext db, ILogger<EducationsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        userId ??= CurrentUserId;

        var educations = userId.HasValue
            ? await _db.Educations.Where(e => e.UserId == userId.Value).ToListAsync()
            : await _db.Educations.ToListAsync();

        var response = educations.Select(e => new EducationResponseDto
        {
            Id = e.Id,
            InstitutionName = e.InstitutionName,
            DegreeType = e.DegreeType,
            FieldOfStudy = e.FieldOfStudy,
            Specialization = e.Specialization,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Status = e.Status,
            City = e.City,
            DiplomaFileUrl = e.DiplomaFileUrl,
            UserId = e.UserId
        }).ToList();

        return Ok(ApiResponse<List<EducationResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var e = await _db.Educations.FindAsync(id);
        if (e == null) return NotFound(ApiResponse<EducationResponseDto>.Error("Education not found"));

        var response = new EducationResponseDto
        {
            Id = e.Id,
            InstitutionName = e.InstitutionName,
            DegreeType = e.DegreeType,
            FieldOfStudy = e.FieldOfStudy,
            Specialization = e.Specialization,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Status = e.Status,
            City = e.City,
            DiplomaFileUrl = e.DiplomaFileUrl,
            UserId = e.UserId
        };

        return Ok(ApiResponse<EducationResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEducationDto dto)
    {
        var edu = new Education
        {
            InstitutionName = dto.InstitutionName,
            DegreeType = dto.DegreeType,
            FieldOfStudy = dto.FieldOfStudy,
            Specialization = dto.Specialization,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = dto.Status,
            City = dto.City,
            DiplomaFileUrl = dto.DiplomaFileUrl,
            UserId = RequiredUserId
        };

        _db.Educations.Add(edu);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created education {Id}", edu.Id);

        var response = new EducationResponseDto
        {
            Id = edu.Id,
            InstitutionName = edu.InstitutionName,
            DegreeType = edu.DegreeType,
            FieldOfStudy = edu.FieldOfStudy,
            Specialization = edu.Specialization,
            StartDate = edu.StartDate,
            EndDate = edu.EndDate,
            Status = edu.Status,
            City = edu.City,
            DiplomaFileUrl = edu.DiplomaFileUrl,
            UserId = edu.UserId
        };

        return Created($"/api/educations/{edu.Id}", ApiResponse<EducationResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEducationDto dto)
    {
        var edu = await _db.Educations.FindAsync(id);
        if (edu == null) return NotFound(ApiResponse<EducationResponseDto>.Error("Education not found"));

        edu.InstitutionName = dto.InstitutionName;
        edu.DegreeType = dto.DegreeType;
        edu.FieldOfStudy = dto.FieldOfStudy;
        edu.Specialization = dto.Specialization;
        edu.StartDate = dto.StartDate;
        edu.EndDate = dto.EndDate;
        edu.Status = dto.Status;
        edu.City = dto.City;
        edu.DiplomaFileUrl = dto.DiplomaFileUrl;

        await _db.SaveChangesAsync();

        var response = new EducationResponseDto
        {
            Id = edu.Id,
            InstitutionName = edu.InstitutionName,
            DegreeType = edu.DegreeType,
            FieldOfStudy = edu.FieldOfStudy,
            Specialization = edu.Specialization,
            StartDate = edu.StartDate,
            EndDate = edu.EndDate,
            Status = edu.Status,
            City = edu.City,
            DiplomaFileUrl = edu.DiplomaFileUrl,
            UserId = edu.UserId
        };

        return Ok(ApiResponse<EducationResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var edu = await _db.Educations.FindAsync(id);
        if (edu == null) return NotFound(ApiResponse<object>.Error("Education not found"));

        _db.Educations.Remove(edu);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Deleted education {Id}", id);
        return NoContent();
    }
}
