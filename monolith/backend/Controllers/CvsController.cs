using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator;
using CV_Generator.Data;
using CV_Generator.Models;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CvsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CvsController(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var cvs = await _db.Cvs.Where(c => c.UserId == userId.Value).Include(c => c.Versions).ToListAsync();
        return Ok(ApiResponse<List<Cv>>.Ok(cvs));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cv = await _db.Cvs.Include(c => c.Versions).FirstOrDefaultAsync(c => c.Id == id);
        if (cv == null) return NotFound(ApiResponse<Cv>.Error("CV not found"));
        return Ok(ApiResponse<Cv>.Ok(cv));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Cv cv)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        cv.UserId = userId.Value;
        cv.CreatedAt = DateTime.UtcNow;
        _db.Cvs.Add(cv);
        await _db.SaveChangesAsync();
        return Created($"/api/cv/{cv.Id}", ApiResponse<Cv>.Created(cv));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cv = await _db.Cvs.FindAsync(id);
        if (cv == null) return NotFound();
        _db.Cvs.Remove(cv);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
