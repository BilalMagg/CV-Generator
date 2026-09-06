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
    private readonly IApplicationService _applications;
    private readonly IContactService _contacts;
    private readonly IServiceScopeFactory _scopeFactory;

    public CompaniesController(
        ICurrentUserService currentUser,
        AppDbContext db,
        ILogger<CompaniesController> logger,
        IApplicationService applications,
        IContactService contacts,
        IServiceScopeFactory scopeFactory)
        : base(currentUser)
    {
        _db = db;
        _logger = logger;
        _applications = applications;
        _contacts = contacts;
        _scopeFactory = scopeFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? country,
        [FromQuery] string? city,
        [FromQuery] int? minApps,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var query = _db.Companies.AsNoTracking().Where(c => c.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Location != null && c.Location.ToLower().Contains(term)) ||
                (c.Sector != null && c.Sector.ToLower().Contains(term)) ||
                (c.Country != null && c.Country.ToLower().Contains(term)) ||
                (c.Note != null && c.Note.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            var countryTerm = country.Trim();
            query = query.Where(c => c.Country == countryTerm);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityTerm = city.Trim().ToLower();
            query = query.Where(c => c.Location != null && c.Location.ToLower().Contains(cityTerm));
        }

        // Application aggregates per company (matched by normalized name, same rule as the UI).
        // Personal-scale data: one companies query + one lightweight applications projection,
        // merged in memory to keep sorting by aggregates trivial.
        var matched = await query.OrderBy(c => c.Name).ToListAsync();
        var appRows = await _db.Applications.AsNoTracking()
            .Where(a => a.CandidateId == userId)
            .Select(a => new { a.CompanyName, a.AppliedAt })
            .ToListAsync();

        var contactRows = await _db.Contacts.AsNoTracking()
            .Where(co => co.UserId == userId && co.Company != null)
            .Select(co => new { co.Company })
            .ToListAsync();

        var stats = new Dictionary<string, (int Count, DateTime? Last)>(StringComparer.Ordinal);
        foreach (var row in appRows)
        {
            if (string.IsNullOrWhiteSpace(row.CompanyName)) continue;
            var key = row.CompanyName.Trim().ToLower();
            var (count, last) = stats.TryGetValue(key, out var s) ? s : (0, null);
            stats[key] = (
                count + 1,
                last.HasValue && row.AppliedAt.HasValue
                    ? (row.AppliedAt.Value > last.Value ? row.AppliedAt : last)
                    : (row.AppliedAt ?? last)
            );
        }

        var contactCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in contactRows)
        {
            if (string.IsNullOrWhiteSpace(row.Company)) continue;
            var key = row.Company.Trim().ToLower();
            contactCounts[key] = contactCounts.GetValueOrDefault(key) + 1;
        }

        var enriched = matched.Select(c =>
        {
            var dto = Map(c);
            var stat = stats.GetValueOrDefault(c.Name.Trim().ToLower(), (0, null));
            dto.ApplicationsCount = stat.Count;
            dto.LastAppliedAt = stat.Last;
            dto.ContactsCount = contactCounts.GetValueOrDefault(c.Name.Trim().ToLower());
            return dto;
        });

        if (minApps.HasValue)
            enriched = enriched.Where(c => c.ApplicationsCount >= minApps.Value);

        var descending = !string.Equals(sortDir?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);
        enriched = (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "apps" or "applications" => descending
                ? enriched.OrderByDescending(c => c.ApplicationsCount).ThenBy(c => c.Name)
                : enriched.OrderBy(c => c.ApplicationsCount).ThenBy(c => c.Name),
            "applied" or "lastapplied" => descending
                ? enriched.OrderByDescending(c => c.LastAppliedAt ?? DateTime.MinValue)
                : enriched.OrderBy(c => c.LastAppliedAt ?? DateTime.MinValue),
            _ => descending
                ? enriched.OrderByDescending(c => c.Name)
                : enriched.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
        };

        var filteredTotal = enriched.Count();
        var items = enriched
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(ApiResponse<CompanyListResponse>.Ok(new CompanyListResponse { Items = items, Total = filteredTotal }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetUserId();
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<CompanyDto>.Error("Company not found"));

        var dto = Map(company);
        (dto.ApplicationsCount, dto.LastAppliedAt) = await GetCompanyStatsAsync(userId, company.Name);
        dto.ContactsCount = await _db.Contacts.CountAsync(co =>
            co.UserId == userId && co.Company != null &&
            co.Company.Trim().ToLower() == company.Name.Trim().ToLower());

        return Ok(ApiResponse<CompanyDto>.Ok(dto));
    }

    /// <summary>All of the user's applications at this company (normalized name match).</summary>
    [HttpGet("{id}/applications")]
    public async Task<IActionResult> GetApplications(Guid id)
    {
        var userId = GetUserId();
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<object>.Error("Company not found"));

        var apps = await _applications.GetApplicationsByCompanyAsync(userId, company.Name);
        return Ok(ApiResponse<List<ApplicationResponseDto>>.Ok(apps));
    }

    /// <summary>Contacts whose company matches this one (normalized name match).</summary>
    [HttpGet("{id}/contacts")]
    public async Task<IActionResult> GetContacts(Guid id)
    {
        var userId = GetUserId();
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<object>.Error("Company not found"));

        var contacts = await _contacts.GetByCompanyAsync(userId, company.Name);
        return Ok(ApiResponse<List<ContactDto>>.Ok(contacts));
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
            Country = string.IsNullOrWhiteSpace(dto.Country) ? "Morocco" : dto.Country.Trim(),
            LocationUrl = dto.LocationUrl,
            Region = dto.Region,
            Sector = dto.Sector,
            FoundedYear = dto.FoundedYear,
            LinkedInUrl = dto.LinkedInUrl,
            Size = dto.Size,
            Note = dto.Note,
            Description = dto.Description
        };
        _db.Companies.Add(company);
        await _db.SaveChangesAsync();
        SearchSyncHelper.TriggerSync(_scopeFactory, company.UserId, _logger, "Company.Create");

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
        if (dto.Country != null)
        {
            var country = dto.Country.Trim();
            company.Country = country.Length == 0 ? "Morocco" : country;
        }
        if (dto.LocationUrl != null) company.LocationUrl = dto.LocationUrl;
        if (dto.Region != null) company.Region = dto.Region;
        if (dto.Sector != null) company.Sector = dto.Sector;
        if (dto.FoundedYear.HasValue) company.FoundedYear = dto.FoundedYear;
        if (dto.LinkedInUrl != null) company.LinkedInUrl = dto.LinkedInUrl;
        if (dto.Size != null) company.Size = dto.Size;
        if (dto.Note != null) company.Note = dto.Note;
        if (dto.Description != null) company.Description = dto.Description;
        company.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        SearchSyncHelper.TriggerSync(_scopeFactory, company.UserId, _logger, "Company.Update");
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
        SearchSyncHelper.TriggerSync(_scopeFactory, company.UserId, _logger, "Company.Delete");
        return Ok(ApiResponse<object>.Ok(null, "Company deleted"));
    }

    private async Task<Company?> FindByNameAsync(Guid userId, string name)
        => await _db.Companies.FirstOrDefaultAsync(c =>
            c.UserId == userId && c.Name.ToLower() == name.ToLower());

    /// <summary>Application count + last applied date for one company (normalized name match).</summary>
    private async Task<(int Count, DateTime? Last)> GetCompanyStatsAsync(Guid userId, string companyName)
    {
        var key = companyName.Trim().ToLower();
        var query = _db.Applications.AsNoTracking()
            .Where(a => a.CandidateId == userId && a.CompanyName.Trim().ToLower() == key);

        var count = await query.CountAsync();
        var last = await query.MaxAsync(a => (DateTime?)a.AppliedAt);

        return (count, last);
    }

    private static CompanyDto Map(Company c) => new()
    {
        Id = c.Id,
        UserId = c.UserId,
        Name = c.Name,
        WebsiteUrl = c.WebsiteUrl,
        Location = c.Location,
        Country = c.Country,
        LocationUrl = c.LocationUrl,
        Region = c.Region,
        Sector = c.Sector,
        FoundedYear = c.FoundedYear,
        LinkedInUrl = c.LinkedInUrl,
        Size = c.Size,
        Note = c.Note,
        Description = c.Description,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
