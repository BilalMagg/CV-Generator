using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[Route("api/companies")]
public class CompaniesController : BaseApiController
{
    private readonly AppDbContext _db;
    private readonly ILogger<CompaniesController> _logger;

    public CompaniesController(ICurrentUserService currentUser, AppDbContext db, ILogger<CompaniesController> logger)
        : base(currentUser)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        var userId = GetUserId();
        var query = _db.Companies.AsNoTracking().Where(c => c.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Location != null && c.Location.ToLower().Contains(term)) ||
                (c.Note != null && c.Note.ToLower().Contains(term)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CompanyDto
            {
                Id = c.Id,
                UserId = c.UserId,
                Name = c.Name,
                WebsiteUrl = c.WebsiteUrl,
                Location = c.Location,
                LocationUrl = c.LocationUrl,
                Note = c.Note,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<CompanyListResponse>.Ok(new CompanyListResponse { Items = items, Total = total }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetUserId();
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<CompanyDto>.Error("Company not found"));

        return Ok(ApiResponse<CompanyDto>.Ok(Map(company)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCompanyDto dto)
    {
        var userId = GetUserId();
        var name = dto.Name?.Trim() ?? "";
        if (name.Length == 0)
            return BadRequest(ApiResponse<CompanyDto>.Error("Company name is required"));
        if (name.Length > 200)
            return BadRequest(ApiResponse<CompanyDto>.Error("Company name cannot exceed 200 characters"));

        var duplicate = await FindByNameAsync(userId, name);
        if (duplicate is not null)
            return Conflict(ApiResponse<CompanyDto>.Error($"'{duplicate.Name}' is already in your list"));

        var company = new Company
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            WebsiteUrl = dto.WebsiteUrl,
            Location = dto.Location,
            LocationUrl = dto.LocationUrl,
            Note = dto.Note
        };
        _db.Companies.Add(company);
        await _db.SaveChangesAsync();

        return Created($"/api/companies/{company.Id}", ApiResponse<CompanyDto>.Created(Map(company)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyDto dto)
    {
        var userId = GetUserId();
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<CompanyDto>.Error("Company not found"));

        if (dto.Name != null)
        {
            var name = dto.Name.Trim();
            if (name.Length == 0)
                return BadRequest(ApiResponse<CompanyDto>.Error("Company name cannot be empty"));
            if (name.Length > 200)
                return BadRequest(ApiResponse<CompanyDto>.Error("Company name cannot exceed 200 characters"));

            var duplicate = await FindByNameAsync(userId, name);
            if (duplicate is not null && duplicate.Id != id)
                return Conflict(ApiResponse<CompanyDto>.Error($"'{duplicate.Name}' is already in your list"));
            company.Name = name;
        }
        if (dto.WebsiteUrl != null) company.WebsiteUrl = dto.WebsiteUrl;
        if (dto.Location != null) company.Location = dto.Location;
        if (dto.LocationUrl != null) company.LocationUrl = dto.LocationUrl;
        if (dto.Note != null) company.Note = dto.Note;
        company.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<CompanyDto>.Ok(Map(company)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<object>.Error("Company not found"));

        _db.Companies.Remove(company);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Company deleted"));
    }

    private async Task<Company?> FindByNameAsync(Guid userId, string name)
        => await _db.Companies.FirstOrDefaultAsync(c =>
            c.UserId == userId && c.Name.ToLower() == name.ToLower());

    private static CompanyDto Map(Company c) => new()
    {
        Id = c.Id,
        UserId = c.UserId,
        Name = c.Name,
        WebsiteUrl = c.WebsiteUrl,
        Location = c.Location,
        LocationUrl = c.LocationUrl,
        Note = c.Note,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
