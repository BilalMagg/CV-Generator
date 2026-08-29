using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator;
using CV_Generator.Models;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AcademicActivitiesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AcademicActivitiesController> _logger;

    public AcademicActivitiesController(AppDbContext db, IServiceScopeFactory scopeFactory, ILogger<AcademicActivitiesController> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? userId)
    {
        userId ??= CurrentUserId;

        var activities = userId.HasValue
            ? await _db.AcademicActivities.Where(a => a.UserId == userId.Value).ToListAsync()
            : await _db.AcademicActivities.ToListAsync();

        var response = activities.Select(a => new AcademicActivityResponseDto
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        }).ToList();

        return Ok(ApiResponse<List<AcademicActivityResponseDto>>.Ok(response));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var a = await _db.AcademicActivities.FindAsync(id);
        if (a == null) return NotFound(ApiResponse<AcademicActivityResponseDto>.Error("Academic activity not found"));

        var response = new AcademicActivityResponseDto
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        };
        return Ok(ApiResponse<AcademicActivityResponseDto>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAcademicActivityDto dto)
    {
        var a = new AcademicActivity
        {
            Title = dto.Title,
            Organization = dto.Organization,
            Description = dto.Description,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            UserId = RequiredUserId
        };

        _db.AcademicActivities.Add(a);
        await _db.SaveChangesAsync();
        SearchSyncHelper.TriggerSync(_scopeFactory, a.UserId, _logger, "AcademicActivity.Create", a.Id);

        var response = new AcademicActivityResponseDto
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        };
        return CreatedAtAction(nameof(GetById), new { id = a.Id }, ApiResponse<AcademicActivityResponseDto>.Created(response));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAcademicActivityDto dto)
    {
        var a = await _db.AcademicActivities.FindAsync(id);
        if (a == null) return NotFound(ApiResponse<AcademicActivityResponseDto>.Error("Academic activity not found"));

        a.Title = dto.Title;
        a.Organization = dto.Organization;
        a.Description = dto.Description;
        a.StartDate = dto.StartDate;
        a.EndDate = dto.EndDate;

        await _db.SaveChangesAsync();
        SearchSyncHelper.TriggerSync(_scopeFactory, a.UserId, _logger, "AcademicActivity.Update", a.Id);

        var response = new AcademicActivityResponseDto
        {
            Id = a.Id,
            Title = a.Title,
            Organization = a.Organization,
            Description = a.Description,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            UserId = a.UserId
        };
        return Ok(ApiResponse<AcademicActivityResponseDto>.Ok(response));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var a = await _db.AcademicActivities.FindAsync(id);
        if (a == null) return NotFound(ApiResponse<object>.Error("Academic activity not found"));

        _db.AcademicActivities.Remove(a);
        await _db.SaveChangesAsync();
        SearchSyncHelper.TriggerSync(_scopeFactory, a.UserId, _logger, "AcademicActivity.Delete", a.Id);

        return NoContent();
    }
}
