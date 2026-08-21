using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator;
using CV_Generator.Data;
using CV_Generator.Models;

namespace CV_Generator.Controllers;

[ApiController]
[Authorize]
[Route("api/cv-versions")]
public class CvVersionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CvVersionsController(AppDbContext db) => _db = db;

    [HttpGet("{cvId}")]
    public async Task<IActionResult> GetByCvId(Guid cvId)
    {
        var versions = await _db.CvVersions.Where(v => v.CvId == cvId).Include(v => v.Sections).ToListAsync();
        return Ok(ApiResponse<List<CvVersion>>.Ok(versions));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CvVersion version)
    {
        version.CreatedAt = DateTime.UtcNow;
        _db.CvVersions.Add(version);
        await _db.SaveChangesAsync();
        return Created($"/api/cv-versions/{version.Id}", ApiResponse<CvVersion>.Created(version));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var version = await _db.CvVersions.FindAsync(id);
        if (version == null) return NotFound();
        _db.CvVersions.Remove(version);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
