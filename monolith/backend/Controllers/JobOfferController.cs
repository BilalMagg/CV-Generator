using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CV_Generator;
using CV_Generator.Data;
using CV_Generator.Hubs;
using CV_Generator.Models;

namespace CV_Generator.Controllers;

[ApiController]
[Route("api/job-offers")]
public class JobOffersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<JobHub> _hub;
    private readonly ILogger<JobOffersController> _logger;

    public JobOffersController(AppDbContext db, IHubContext<JobHub> hub, ILogger<JobOffersController> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.JobOffers.Include(j => j.Skills).OrderByDescending(j => j.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var job = await _db.JobOffers.Include(j => j.Skills).Include(j => j.Responsibilities).Include(j => j.Benefits).FirstOrDefaultAsync(j => j.Id == id);
        if (job == null) return NotFound(ApiResponse<object>.Error("Job offer not found"));
        return Ok(ApiResponse<JobOffer>.Ok(job));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JobOffer jobOffer)
    {
        jobOffer.CreatedAt = DateTime.UtcNow;
        jobOffer.UpdatedAt = DateTime.UtcNow;
        _db.JobOffers.Add(jobOffer);
        await _db.SaveChangesAsync();
        return Created($"/api/job-offers/{jobOffer.Id}", ApiResponse<JobOffer>.Created(jobOffer));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var job = await _db.JobOffers.FindAsync(id);
        if (job == null) return NotFound();
        _db.JobOffers.Remove(job);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.JobOffers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(j => j.JobRole.Contains(keyword) || j.EnterpriseName.Contains(keyword));
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(j => j.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new { items, total, page, pageSize }));
    }
}
