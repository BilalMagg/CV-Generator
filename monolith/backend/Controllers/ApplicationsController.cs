using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator;
using CV_Generator.Dto;
using CV_Generator.Data;
using CV_Generator.Models;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ApplicationsController> _logger;
    private readonly ICurrentUserService _currentUser;

    public ApplicationsController(AppDbContext db, ICurrentUserService currentUser, ILogger<ApplicationsController> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var candidateId = _currentUser.UserId;
        if (candidateId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var query = _db.Applications.Where(a => a.CandidateId == candidateId.Value);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(a => a.AppliedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var app = await _db.Applications.Include(a => a.StatusHistory).FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound(ApiResponse<object>.Error("Application not found"));
        return Ok(ApiResponse<Application>.Ok(app));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationDto dto)
    {
        var candidateId = _currentUser.UserId;
        if (candidateId == null) return Unauthorized(ApiResponse<object>.Error("Unable to determine user identity"));

        var app = new Application
        {
            CandidateId = candidateId.Value,
            CompanyName = dto.CompanyName,
            PositionTitle = dto.PositionTitle,
            OfferSource = dto.OfferSource,
            Status = ApplicationStatus.PENDING,
            AppliedAt = DateTime.UtcNow,
            IsSaved = false,
        };
        _db.Applications.Add(app);
        await _db.SaveChangesAsync();
        return Created($"/api/applications/{app.Id}", ApiResponse<Application>.Created(app));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var app = await _db.Applications.FindAsync(id);
        if (app == null) return NotFound(ApiResponse<object>.Error("Application not found"));
        _db.Applications.Remove(app);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
