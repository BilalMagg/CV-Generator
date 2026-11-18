using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

[ApiController]
[Authorize]
[Route("api/applications/domains")]
public class ApplicationDomainsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ApplicationDomainsController> _logger;

    public ApplicationDomainsController(
        AppDbContext db,
        ICurrentUserService currentUser,
        ILogger<ApplicationDomainsController> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid? UserId => _currentUser.UserId;

    /// <summary>
    /// Gets all application domain categories for the current user.
    /// Auto-seeds the 5 default domains if the user has no categories yet.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unauthorized"));
        var userId = UserId.Value;

        var domains = await _db.ApplicationDomains
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        if (domains.Count == 0)
        {
            var now = DateTime.UtcNow;
            var seeded = ApplicationDomain.DefaultDomains
                .Select((name, i) => new ApplicationDomain
                {
                    UserId = userId,
                    Name = name,
                    CreatedAt = now.AddSeconds(i),
                    UpdatedAt = now.AddSeconds(i)
                })
                .ToList();

            _db.ApplicationDomains.AddRange(seeded);
            await _db.SaveChangesAsync();
            domains = seeded;
        }

        var counts = await _db.Applications
            .Where(a => a.CandidateId == userId && a.Domain != null)
            .GroupBy(a => a.Domain!)
            .Select(g => new { Domain = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Domain, x => x.Count, StringComparer.OrdinalIgnoreCase);

        var dtos = domains.Select(d => new ApplicationDomainDto(
            d.Id,
            d.Name,
            counts.GetValueOrDefault(d.Name, 0),
            d.CreatedAt
        )).ToList();

        return Ok(ApiResponse<List<ApplicationDomainDto>>.Ok(dtos));
    }

    /// <summary>
    /// Check whether a domain name already exists or has similar existing names.
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> CheckSimilarity([FromQuery] string name)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unauthorized"));
        if (string.IsNullOrWhiteSpace(name))
            return Ok(ApiResponse<ApplicationDomainCheckDto>.Ok(new ApplicationDomainCheckDto(false, [])));

        var query = name.Trim();
        var domains = await _db.ApplicationDomains
            .Where(d => d.UserId == UserId.Value)
            .Select(d => d.Name)
            .ToListAsync();

        var exact = domains.Any(d => string.Equals(d, query, StringComparison.OrdinalIgnoreCase));
        var similar = FindSimilarNames(query, domains);

        return Ok(ApiResponse<ApplicationDomainCheckDto>.Ok(new ApplicationDomainCheckDto(exact, similar)));
    }

    /// <summary>
    /// Creates a new domain category for the current user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationDomainDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unauthorized"));
        var userId = UserId.Value;

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(ApiResponse<object>.Error("Category name is required"));

        var name = dto.Name.Trim();
        if (name.Length > 100)
            return BadRequest(ApiResponse<object>.Error("Category name cannot exceed 100 characters"));

        var exists = await _db.ApplicationDomains
            .AnyAsync(d => d.UserId == userId && d.Name.ToLower() == name.ToLower());
        if (exists)
            return Conflict(ApiResponse<object>.Error($"A category named '{name}' already exists"));

        var domain = new ApplicationDomain
        {
            UserId = userId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ApplicationDomains.Add(domain);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created application domain {Name} ({Id}) for user {User}", domain.Name, domain.Id, userId);

        return Ok(ApiResponse<ApplicationDomainDto>.Ok(new ApplicationDomainDto(domain.Id, domain.Name, 0, domain.CreatedAt)));
    }

    /// <summary>
    /// Renames an existing application domain category, updating all linked applications.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApplicationDomainDto dto)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unauthorized"));
        var userId = UserId.Value;

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(ApiResponse<object>.Error("Category name is required"));

        var newName = dto.Name.Trim();
        if (newName.Length > 100)
            return BadRequest(ApiResponse<object>.Error("Category name cannot exceed 100 characters"));

        var domain = await _db.ApplicationDomains
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (domain == null) return NotFound(ApiResponse<object>.Error("Category not found"));

        if (!string.Equals(domain.Name, newName, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = await _db.ApplicationDomains
                .AnyAsync(d => d.UserId == userId && d.Id != id && d.Name.ToLower() == newName.ToLower());
            if (conflict)
                return Conflict(ApiResponse<object>.Error($"A category named '{newName}' already exists"));

            var oldName = domain.Name;
            domain.Name = newName;
            domain.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Cascade update on applications
            await _db.Applications
                .Where(a => a.CandidateId == userId && a.Domain == oldName)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Domain, newName));

            _logger.LogInformation("Renamed domain {Old} to {New} for user {User}", oldName, newName, userId);
        }

        var appCount = await _db.Applications
            .CountAsync(a => a.CandidateId == userId && a.Domain == domain.Name);

        return Ok(ApiResponse<ApplicationDomainDto>.Ok(new ApplicationDomainDto(domain.Id, domain.Name, appCount, domain.CreatedAt)));
    }

    /// <summary>
    /// Deletes an application domain category, clearing it on existing applications.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (UserId == null) return Unauthorized(ApiResponse<object>.Error("Unauthorized"));
        var userId = UserId.Value;

        var domain = await _db.ApplicationDomains
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (domain == null) return NotFound(ApiResponse<object>.Error("Category not found"));

        var oldName = domain.Name;

        // Cascade clear on applications
        await _db.Applications
            .Where(a => a.CandidateId == userId && a.Domain == oldName)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Domain, (string?)null));

        _db.ApplicationDomains.Remove(domain);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Deleted application domain {Name} ({Id}) for user {User}", oldName, id, userId);

        return Ok(ApiResponse<object>.Ok(null));
    }

    private static List<string> FindSimilarNames(string query, List<string> existing)
    {
        var results = new List<string>();
        var queryTokens = Tokenize(query);
        var queryNorm = query.ToLowerInvariant();

        foreach (var name in existing)
        {
            if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase)) continue;

            var nameNorm = name.ToLowerInvariant();
            var nameTokens = Tokenize(name);

            // Substring match
            if (queryNorm.Length >= 3 && nameNorm.Contains(queryNorm))
            {
                results.Add(name);
                continue;
            }
            if (nameNorm.Length >= 3 && queryNorm.Contains(nameNorm))
            {
                results.Add(name);
                continue;
            }

            // Token overlap (e.g. "Data & AI" vs "Data Science" shares "data")
            var hasCommonToken = queryTokens.Any(qt => nameTokens.Contains(qt));
            if (hasCommonToken)
            {
                results.Add(name);
                continue;
            }

            // Edit distance for typos / plurals (e.g. "System" vs "Systems")
            if (Math.Abs(queryNorm.Length - nameNorm.Length) <= 2 && LevenshteinDistance(queryNorm, nameNorm) <= 2)
            {
                results.Add(name);
            }
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static HashSet<string> Tokenize(string text)
    {
        var separators = new[] { ' ', '&', '+', '/', '-', '_', ',', '.', '(', ')' };
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "and", "or", "the", "for", "in", "of", "to", "a", "an", "&" };

        var tokens = text.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length >= 2 && !stopWords.Contains(t));

        return new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}
