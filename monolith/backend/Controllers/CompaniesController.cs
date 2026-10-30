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
        [FromQuery] string? sector,
        [FromQuery] bool? hasWebsite,
        [FromQuery] bool? researched,
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

        if (!string.IsNullOrWhiteSpace(sector))
        {
            var sectorTerm = sector.Trim().ToLower();
            query = query.Where(c => c.Sector != null && c.Sector.ToLower().Contains(sectorTerm));
        }

        if (hasWebsite.HasValue)
        {
            query = hasWebsite.Value
                ? query.Where(c => c.WebsiteUrl != null && c.WebsiteUrl.Length > 0)
                : query.Where(c => c.WebsiteUrl == null || c.WebsiteUrl.Length == 0);
        }

        if (researched.HasValue)
        {
            query = researched.Value
                ? query.Where(c => c.ResearchSource == "web-research")
                : query.Where(c => c.ResearchSource != "web-research" || c.ResearchSource == null);
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
            "researched" or "lastresearched" => descending
                ? enriched.OrderByDescending(c => c.ResearchUpdatedAt ?? DateTime.MinValue).ThenBy(c => c.Name)
                : enriched.OrderBy(c => c.ResearchUpdatedAt ?? DateTime.MinValue).ThenBy(c => c.Name),
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

    /// <summary>Distinct non-empty sectors for filter dropdowns.</summary>
    [HttpGet("sectors")]
    public async Task<IActionResult> GetSectors()
    {
        var userId = GetUserId();
        var sectors = await _db.Companies.AsNoTracking()
            .Where(c => c.UserId == userId && c.Sector != null && c.Sector.Trim().Length > 0)
            .Select(c => c.Sector!.Trim())
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        return Ok(ApiResponse<List<string>>.Ok(sectors));
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

    // ── Portal/account credentials kept per company ────────────────────────────

    [HttpGet("{id}/credentials")]
    public async Task<IActionResult> GetCredentials(Guid id)
    {
        var userId = GetUserId();
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<object>.Error("Company not found"));

        var creds = await _db.CompanyCredentials.AsNoTracking()
            .Where(x => x.CompanyId == id)
            .OrderBy(x => x.Label)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<CompanyCredentialDto>>.Ok(creds.Select(MapCredential).ToList()));
    }

    [HttpPost("{id}/credentials")]
    public async Task<IActionResult> CreateCredential(Guid id, [FromBody] UpsertCompanyCredentialDto dto)
    {
        var userId = GetUserId();
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<object>.Error("Company not found"));

        if (ValidateCredential(dto) is { } error) return BadRequest(ApiResponse<object>.Error(error));

        var cred = new CompanyCredential
        {
            CompanyId = id,
            UserId = userId,
            Label = Normalize(dto.Label) ?? "Account",
            Url = Normalize(dto.Url),
            Username = Normalize(dto.Username),
            Password = dto.Password,
            Notes = Normalize(dto.Notes)
        };

        _db.CompanyCredentials.Add(cred);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Credential saved for company {Company}", id);
        return Ok(ApiResponse<CompanyCredentialDto>.Ok(MapCredential(cred)));
    }

    [HttpPut("{id}/credentials/{credId}")]
    public async Task<IActionResult> UpdateCredential(Guid id, Guid credId, [FromBody] UpsertCompanyCredentialDto dto)
    {
        var userId = GetUserId();
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<object>.Error("Company not found"));

        var cred = await _db.CompanyCredentials
            .FirstOrDefaultAsync(x => x.Id == credId && x.CompanyId == id);
        if (cred is null) return NotFound(ApiResponse<object>.Error("Credential not found"));

        if (ValidateCredential(dto) is { } error) return BadRequest(ApiResponse<object>.Error(error));

        cred.Label = Normalize(dto.Label) ?? cred.Label;
        cred.Url = Normalize(dto.Url);
        cred.Username = Normalize(dto.Username);
        cred.Password = dto.Password;
        cred.Notes = Normalize(dto.Notes);
        cred.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<CompanyCredentialDto>.Ok(MapCredential(cred)));
    }

    [HttpDelete("{id}/credentials/{credId}")]
    public async Task<IActionResult> DeleteCredential(Guid id, Guid credId)
    {
        var userId = GetUserId();
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<object>.Error("Company not found"));

        var cred = await _db.CompanyCredentials
            .FirstOrDefaultAsync(x => x.Id == credId && x.CompanyId == id);
        if (cred is null) return NotFound(ApiResponse<object>.Error("Credential not found"));

        _db.CompanyCredentials.Remove(cred);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private static string? ValidateCredential(UpsertCompanyCredentialDto dto)
    {
        var hasContent = !string.IsNullOrWhiteSpace(dto.Label)
            || !string.IsNullOrWhiteSpace(dto.Url)
            || !string.IsNullOrWhiteSpace(dto.Username)
            || !string.IsNullOrWhiteSpace(dto.Password)
            || !string.IsNullOrWhiteSpace(dto.Notes);
        if (!hasContent) return "Add at least a username, password or portal URL";

        if (dto.Label?.Trim().Length > 200) return "Label cannot exceed 200 characters";
        if (dto.Url?.Trim().Length > 500) return "Portal URL cannot exceed 500 characters";
        if (dto.Username?.Trim().Length > 300) return "Username cannot exceed 300 characters";
        if (dto.Password?.Length > 500) return "Password cannot exceed 500 characters";
        if (dto.Notes?.Trim().Length > 2000) return "Notes cannot exceed 2000 characters";
        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

        var logo = await ResolveLogoAsync(userId, dto.LogoImageId, dto.LogoUrl);
        if (logo.Error != null) return BadRequest(ApiResponse<CompanyDto>.Error(logo.Error));

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
            Specialties = dto.Specialties,
            LogoImageId = dto.LogoImageId,
            LogoUrl = logo.LogoUrl,
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

        var oldName = company.Name;
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
        var renamed = !company.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase);
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
        if (dto.Specialties != null) company.Specialties = dto.Specialties;
        if (dto.LogoUrl != null)
        {
            var logo = await ResolveLogoAsync(userId, dto.LogoImageId, dto.LogoUrl);
            if (logo.Error != null) return BadRequest(ApiResponse<CompanyDto>.Error(logo.Error));
            company.LogoUrl = logo.LogoUrl;
            company.LogoImageId = dto.LogoImageId;
        }
        if (dto.Note != null) company.Note = dto.Note;
        if (dto.Description != null) company.Description = dto.Description;
        company.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        if (renamed)
        {
            await _db.Contacts
                .Where(c => c.UserId == userId && c.Company != null &&
                            c.Company.Trim().ToLower() == oldName.Trim().ToLower())
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Company, company.Name));
            await _db.Applications
                .Where(a => a.CandidateId == userId && a.CompanyName.Trim().ToLower() == oldName.Trim().ToLower())
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.CompanyName, company.Name));
        }
        SearchSyncHelper.TriggerSync(_scopeFactory, company.UserId, _logger, "Company.Update");
        return Ok(ApiResponse<CompanyDto>.Ok(Map(company)));
    }

    [HttpGet("merge/candidates")]
    public async Task<IActionResult> MergeCandidates()
    {
        var userId = GetUserId();
        var companies = await _db.Companies.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
        if (companies.Count < 2)
            return Ok(ApiResponse<List<CompanyMergeCandidateDto>>.Ok([]));

        var appsByName = await _db.Applications.AsNoTracking()
            .Where(a => a.CandidateId == userId)
            .GroupBy(a => a.CompanyName.Trim().ToLower())
            .ToDictionaryAsync(g => g.Key, g => g.Count());

        var result = new List<CompanyMergeCandidateDto>();
        for (var i = 0; i < companies.Count; i++)
        {
            for (var j = i + 1; j < companies.Count; j++)
            {
                var a = companies[i];
                var b = companies[j];
                var reason = ClassifyMergeMatch(a.Name, b.Name);
                if (reason is null) continue;
                result.Add(new CompanyMergeCandidateDto
                {
                    IdA = a.Id,
                    IdB = b.Id,
                    NameA = a.Name,
                    NameB = b.Name,
                    Reason = reason,
                    AppsA = appsByName.GetValueOrDefault(a.Name.Trim().ToLower()),
                    AppsB = appsByName.GetValueOrDefault(b.Name.Trim().ToLower()),
                    CreatedA = a.CreatedAt,
                    CreatedB = b.CreatedAt,
                });
            }
        }
        return Ok(ApiResponse<List<CompanyMergeCandidateDto>>.Ok(result));
    }

    /// <summary>"same-base" | "prefix-match" | null (unlikely duplicates).</summary>
    private static string? ClassifyMergeMatch(string nameA, string nameB)
    {
        var a = MergeBaseName(nameA);
        var b = MergeBaseName(nameB);
        if (a.Length == 0 || b.Length == 0) return null;
        if (a == b) return "same-base";
        // prefix match: "inetum" ⊂ "inetum maroc" / "inetummorocco" (the tail is smaller than the head)
        if (a.Length >= 3 && b.StartsWith(a) && a.Length * 2 >= b.Length) return "prefix-match";
        if (b.Length >= 3 && a.StartsWith(b) && b.Length * 2 >= a.Length) return "prefix-match";
        return null;
    }

    /// <summary>Lowercased, diacritic-stripped base name: drops generic alias tokens (maroc/morocco/sa/…) and punctuation.</summary>
    private static readonly string[] MergeAliasTokens = ["maroc", "morocco", "offshore", "sarl", "sa", "sasu", "llc", "inc", "gmbh", "group", "marocaine"];

    private static string MergeBaseName(string name)
    {
        var normalized = name.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }
        var tokens = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        tokens.RemoveAll(t => MergeAliasTokens.Contains(t));
        tokens.RemoveAll(t => t.Length == 1);
        return string.Join(' ', tokens).Trim();
    }

    [HttpGet("merge/preview")]
    public async Task<IActionResult> GetMergePreview([FromQuery] Guid companyAId, [FromQuery] Guid companyBId)
    {
        var userId = GetUserId();
        if (companyAId == companyBId)
            return BadRequest(ApiResponse<object>.Error("Cannot merge a company with itself"));

        var companyA = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyAId && c.UserId == userId);
        var companyB = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyBId && c.UserId == userId);
        if (companyA is null || companyB is null)
            return NotFound(ApiResponse<object>.Error("One or both companies not found"));

        var dtoA = Map(companyA);
        var dtoB = Map(companyB);

        var appsA = await _applications.GetApplicationsByCompanyAsync(userId, companyA.Name);
        var appsB = await _applications.GetApplicationsByCompanyAsync(userId, companyB.Name);
        dtoA.ApplicationsCount = appsA.Count;
        dtoB.ApplicationsCount = appsB.Count;

        var contactsA = await _contacts.GetByCompanyAsync(userId, companyA.Name);
        var contactsB = await _contacts.GetByCompanyAsync(userId, companyB.Name);
        dtoA.ContactsCount = contactsA.Count;
        dtoB.ContactsCount = contactsB.Count;

        var credsA = await _db.CompanyCredentials.AsNoTracking()
            .Where(x => x.CompanyId == companyAId)
            .OrderBy(x => x.Label)
            .ToListAsync();
        var credsB = await _db.CompanyCredentials.AsNoTracking()
            .Where(x => x.CompanyId == companyBId)
            .OrderBy(x => x.Label)
            .ToListAsync();

        // Detect duplicate applications (matching job offer ID or normalized position title)
        var dupApps = new List<DuplicateApplicationPairDto>();
        var usedBAppIds = new HashSet<Guid>();
        foreach (var a in appsA)
        {
            var normA = FingerprintHelper.Normalize(a.PositionTitle);
            var matchB = appsB.FirstOrDefault(b => !usedBAppIds.Contains(b.Id) &&
                ((a.JobOfferId.HasValue && b.JobOfferId.HasValue && a.JobOfferId == b.JobOfferId) ||
                 (!string.IsNullOrEmpty(normA) && normA == FingerprintHelper.Normalize(b.PositionTitle))));
            if (matchB != null)
            {
                usedBAppIds.Add(matchB.Id);
                dupApps.Add(new DuplicateApplicationPairDto
                {
                    ApplicationA = a,
                    ApplicationB = matchB,
                    Reason = a.JobOfferId.HasValue && a.JobOfferId == matchB.JobOfferId
                        ? "Identical job offer link"
                        : $"Matching position title: '{a.PositionTitle}'"
                });
            }
        }

        // Detect duplicate contacts (matching email or matching contact name)
        var dupContacts = new List<DuplicateContactPairDto>();
        var usedBContactIds = new HashSet<Guid>();
        foreach (var ca in contactsA)
        {
            var emailA = ca.Email?.Trim().ToLowerInvariant();
            var nameA = ca.Name?.Trim().ToLowerInvariant();

            var matchB = contactsB.FirstOrDefault(cb => !usedBContactIds.Contains(cb.Id) &&
                ((!string.IsNullOrEmpty(emailA) && !string.IsNullOrEmpty(cb.Email) && emailA == cb.Email.Trim().ToLowerInvariant()) ||
                 (!string.IsNullOrEmpty(nameA) && !string.IsNullOrEmpty(cb.Name) && nameA == cb.Name.Trim().ToLowerInvariant())));

            if (matchB != null)
            {
                usedBContactIds.Add(matchB.Id);
                dupContacts.Add(new DuplicateContactPairDto
                {
                    ContactA = ca,
                    ContactB = matchB,
                    Reason = !string.IsNullOrEmpty(emailA) && emailA == matchB.Email?.Trim().ToLowerInvariant()
                        ? $"Same email address: {ca.Email}"
                        : $"Same contact name: '{ca.Name}'"
                });
            }
        }

        var oldestCreated = companyA.CreatedAt < companyB.CreatedAt ? companyA.CreatedAt : companyB.CreatedAt;

        var preview = new CompanyMergePreviewDto
        {
            CompanyA = dtoA,
            CompanyB = dtoB,
            ApplicationsA = appsA,
            ApplicationsB = appsB,
            DuplicateApplications = dupApps,
            ContactsA = contactsA,
            ContactsB = contactsB,
            DuplicateContacts = dupContacts,
            CredentialsA = credsA.Select(MapCredential).ToList(),
            CredentialsB = credsB.Select(MapCredential).ToList(),
            OldestCreatedAt = oldestCreated
        };

        return Ok(ApiResponse<CompanyMergePreviewDto>.Ok(preview));
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] CompanyMergeRequestDto dto)
    {
        var userId = GetUserId();
        if (dto.SourceCompanyId == dto.TargetCompanyId)
            return BadRequest(ApiResponse<object>.Error("Source and target companies cannot be the same"));

        var source = await _db.Companies.FirstOrDefaultAsync(c => c.Id == dto.SourceCompanyId && c.UserId == userId);
        var target = await _db.Companies.FirstOrDefaultAsync(c => c.Id == dto.TargetCompanyId && c.UserId == userId);
        if (source is null || target is null)
            return NotFound(ApiResponse<object>.Error("One or both companies not found"));

        var finalName = dto.FinalName?.Trim();
        if (string.IsNullOrWhiteSpace(finalName))
            return BadRequest(ApiResponse<object>.Error("Final company name is required"));
        if (finalName.Length > 200)
            return BadRequest(ApiResponse<object>.Error("Final company name cannot exceed 200 characters"));

        // Ensure no clash with any other company
        var clash = await _db.Companies.FirstOrDefaultAsync(c =>
            c.UserId == userId && c.Id != source.Id && c.Id != target.Id &&
            c.Name.ToLower() == finalName.ToLower());
        if (clash != null)
            return Conflict(ApiResponse<object>.Error($"Another company named '{clash.Name}' already exists"));

        var sourceName = source.Name.Trim();
        var targetName = target.Name.Trim();
        var sourceLower = sourceName.ToLower();
        var targetLower = targetName.ToLower();

        await using var tx = await _db.Database.BeginTransactionAsync();

        // 1. Duplicate Contact Resolutions
        if (dto.ContactResolutions != null)
        {
            foreach (var cr in dto.ContactResolutions)
            {
                var contactA = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == cr.ContactAId && c.UserId == userId);
                var contactB = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == cr.ContactBId && c.UserId == userId);

                if (cr.Mode == "KeepA" && contactB != null && contactA != null)
                {
                    await _db.ApplicationAttempts
                        .Where(a => a.ContactId == contactB.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.ContactId, contactA.Id));
                    _db.Contacts.Remove(contactB);
                }
                else if (cr.Mode == "KeepB" && contactA != null && contactB != null)
                {
                    await _db.ApplicationAttempts
                        .Where(a => a.ContactId == contactA.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.ContactId, contactB.Id));
                    _db.Contacts.Remove(contactA);
                }
            }
            await _db.SaveChangesAsync();
        }

        // Re-link all remaining contacts of both companies to finalName
        var allContacts = await _db.Contacts
            .Where(c => c.UserId == userId && c.Company != null &&
                        (c.Company.Trim().ToLower() == sourceLower || c.Company.Trim().ToLower() == targetLower))
            .ToListAsync();
        foreach (var c in allContacts)
        {
            c.Company = finalName;
        }

        // 2. Duplicate Application Resolutions
        if (dto.ApplicationResolutions != null)
        {
            foreach (var ar in dto.ApplicationResolutions)
            {
                if (ar.Mode == "Merge")
                {
                    var primary = await _db.Applications.FirstOrDefaultAsync(a => a.Id == ar.PrimaryApplicationId && a.CandidateId == userId);
                    var secondary = await _db.Applications.FirstOrDefaultAsync(a => a.Id == ar.SecondaryApplicationId && a.CandidateId == userId);

                    if (primary != null && secondary != null && primary.Id != secondary.Id)
                    {
                        var maxAttempt = await _db.ApplicationAttempts
                            .Where(a => a.ApplicationId == primary.Id)
                            .MaxAsync(a => (int?)a.AttemptNumber) ?? 0;

                        var secAttempts = await _db.ApplicationAttempts
                            .Where(a => a.ApplicationId == secondary.Id)
                            .OrderBy(a => a.AttemptNumber)
                            .ToListAsync();

                        foreach (var att in secAttempts)
                        {
                            att.ApplicationId = primary.Id;
                            att.AttemptNumber = ++maxAttempt;
                        }

                        var secHistories = await _db.ApplicationStatusHistories
                            .Where(h => h.ApplicationId == secondary.Id)
                            .ToListAsync();
                        foreach (var h in secHistories)
                        {
                            h.ApplicationId = primary.Id;
                        }

                        if (!string.IsNullOrWhiteSpace(secondary.Notes))
                        {
                            if (string.IsNullOrWhiteSpace(primary.Notes))
                                primary.Notes = secondary.Notes;
                            else if (!primary.Notes.Contains(secondary.Notes))
                                primary.Notes += $"\n\nMerged application notes:\n{secondary.Notes}";
                        }

                        if (primary.CvVersionId == null && secondary.CvVersionId != null)
                            primary.CvVersionId = secondary.CvVersionId;

                        if (primary.Status == ApplicationStatus.SAVED && secondary.Status != ApplicationStatus.SAVED)
                        {
                            primary.Status = secondary.Status;
                            primary.AppliedAt = secondary.AppliedAt ?? DateTime.UtcNow;
                        }

                        _db.Applications.Remove(secondary);
                    }
                }
            }
            await _db.SaveChangesAsync();
        }

        // Re-link all remaining applications of both companies to finalName and recompute Fingerprint
        var allApps = await _db.Applications
            .Where(a => a.CandidateId == userId &&
                        (a.CompanyName.Trim().ToLower() == sourceLower || a.CompanyName.Trim().ToLower() == targetLower))
            .ToListAsync();
        foreach (var app in allApps)
        {
            app.CompanyName = finalName;
            app.Fingerprint = FingerprintHelper.Compute(finalName, app.PositionTitle);
            app.UpdatedAt = DateTime.UtcNow;
        }

        // 3. Resolve Credentials
        var sourceCreds = await _db.CompanyCredentials.Where(c => c.CompanyId == source.Id).ToListAsync();
        var targetCreds = await _db.CompanyCredentials.Where(c => c.CompanyId == target.Id).ToListAsync();
        var allCreds = sourceCreds.Concat(targetCreds).ToList();

        var keepCredIds = dto.KeepCredentialIds ?? new List<Guid>();
        foreach (var cred in allCreds)
        {
            if (keepCredIds.Contains(cred.Id))
            {
                cred.CompanyId = target.Id;
                cred.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.CompanyCredentials.Remove(cred);
            }
        }

        // 4. Update References in Other Tables
        // CoverLetters
        var coverLetters = await _db.CoverLetters
            .Where(cl => cl.UserId == userId && cl.CompanyId == source.Id)
            .ToListAsync();
        foreach (var cl in coverLetters)
        {
            cl.CompanyId = target.Id;
        }

        // ApplyQueueItems
        var queueItems = await _db.ApplyQueueItems
            .Where(q => q.UserId == userId && (q.CompanyId == source.Id || q.CompanyId == target.Id ||
                        q.CompanyName.Trim().ToLower() == sourceLower || q.CompanyName.Trim().ToLower() == targetLower))
            .ToListAsync();
        foreach (var q in queueItems)
        {
            q.CompanyId = target.Id;
            q.CompanyName = finalName;
        }

        // BulkApplyRows
        var bulkRows = await _db.BulkApplyRows
            .Where(b => (b.CompanyId == source.Id || b.CompanyId == target.Id ||
                         b.CompanyName.Trim().ToLower() == sourceLower || b.CompanyName.Trim().ToLower() == targetLower))
            .ToListAsync();
        foreach (var b in bulkRows)
        {
            b.CompanyId = target.Id;
            b.CompanyName = finalName;
        }

        // EmailDrafts
        var drafts = await _db.EmailDrafts
            .Where(d => d.UserId == userId && d.CompanyName != null &&
                        (d.CompanyName.Trim().ToLower() == sourceLower || d.CompanyName.Trim().ToLower() == targetLower))
            .ToListAsync();
        foreach (var d in drafts)
        {
            d.CompanyName = finalName;
        }

        // Employees
        var employees = await _db.Employees
            .Where(e => e.UserId == userId && e.Company != null &&
                        (e.Company.Trim().ToLower() == sourceLower || e.Company.Trim().ToLower() == targetLower))
            .ToListAsync();
        foreach (var e in employees)
        {
            e.Company = finalName;
        }

        // 5. Update Target Company (earliest CreatedAt used per user requirement)
        target.CreatedAt = source.CreatedAt < target.CreatedAt ? source.CreatedAt : target.CreatedAt;
        target.UpdatedAt = DateTime.UtcNow;
        target.Name = finalName;
        target.WebsiteUrl = dto.WebsiteUrl;
        target.Location = dto.Location;
        target.Country = string.IsNullOrWhiteSpace(dto.Country) ? "Morocco" : dto.Country.Trim();
        target.LocationUrl = dto.LocationUrl;
        target.Region = dto.Region;
        target.Sector = dto.Sector;
        target.FoundedYear = dto.FoundedYear;
        target.LinkedInUrl = dto.LinkedInUrl;
        target.Size = dto.Size;
        target.Specialties = dto.Specialties;
        target.LogoUrl = dto.LogoUrl;
        target.LogoImageId = dto.LogoImageId;
        target.Description = dto.Description;
        target.Note = dto.Note;

        // Merge researched metadata if available
        var combinedEmails = CompanyResearchJson.ParseList(target.EmailsJson)
            .Concat(CompanyResearchJson.ParseList(source.EmailsJson))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        target.EmailsJson = combinedEmails.Count > 0 ? CompanyResearchJson.SerializeList(combinedEmails) : null;

        var combinedPhones = CompanyResearchJson.ParseList(target.PhonesJson)
            .Concat(CompanyResearchJson.ParseList(source.PhonesJson))
            .Distinct()
            .ToList();
        target.PhonesJson = combinedPhones.Count > 0 ? CompanyResearchJson.SerializeList(combinedPhones) : null;

        var combinedSocial = CompanyResearchJson.ParseSocialLinks(target.SocialLinksJson)
            .Concat(CompanyResearchJson.ParseSocialLinks(source.SocialLinksJson))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        target.SocialLinksJson = combinedSocial.Count > 0 ? CompanyResearchJson.SerializeSocialLinks(combinedSocial) : null;

        var combinedFacts = CompanyResearchJson.ParseList(target.CompanyFactsJson)
            .Concat(CompanyResearchJson.ParseList(source.CompanyFactsJson))
            .Distinct()
            .ToList();
        target.CompanyFactsJson = combinedFacts.Count > 0 ? CompanyResearchJson.SerializeList(combinedFacts) : null;

        if (string.IsNullOrWhiteSpace(target.ResearchSource) && !string.IsNullOrWhiteSpace(source.ResearchSource))
        {
            target.ResearchSource = source.ResearchSource;
            target.ResearchLink = source.ResearchLink;
            target.ResearchUpdatedAt = source.ResearchUpdatedAt;
        }

        // Rename source prior to removal to avoid transient unique index clash
        source.Name = $"{source.Name}_merged_{Guid.NewGuid():N}";
        _db.Companies.Remove(source);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        SearchSyncHelper.TriggerSync(_scopeFactory, userId, _logger, "Company.Merge");

        var mapped = Map(target);
        (mapped.ApplicationsCount, mapped.LastAppliedAt) = await GetCompanyStatsAsync(userId, target.Name);
        mapped.ContactsCount = await _db.Contacts.CountAsync(co =>
            co.UserId == userId && co.Company != null && co.Company.Trim().ToLower() == target.Name.Trim().ToLower());

        return Ok(ApiResponse<CompanyDto>.Ok(mapped, "Companies successfully merged"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (company is null) return NotFound(ApiResponse<object>.Error("Company not found"));

        var deletedName = company.Name;
        _db.Companies.Remove(company);
        await _db.SaveChangesAsync();
        await _db.Contacts
            .Where(c => c.UserId == userId && c.Company != null &&
                        c.Company.Trim().ToLower() == deletedName.Trim().ToLower())
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Company, (string?)null));
        SearchSyncHelper.TriggerSync(_scopeFactory, company.UserId, _logger, "Company.Delete");
        return Ok(ApiResponse<object>.Ok(null, "Company deleted"));
    }

    private async Task<Company?> FindByNameAsync(Guid userId, string name)
        => await _db.Companies.FirstOrDefaultAsync(c =>
            c.UserId == userId && c.Name.ToLower() == name.ToLower());

    /// <summary>
    /// Resolves the logo: when the client references a library image, verify ownership and
    /// fall back to the image's stored URL; an explicit URL is kept as-is (empty string clears).
    /// </summary>
    private async Task<(string? Error, string? LogoUrl)> ResolveLogoAsync(Guid userId, Guid? imageId, string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            if (imageId.HasValue)
            {
                var image = await _db.UserImages.FirstOrDefaultAsync(i => i.Id == imageId.Value && i.UserId == userId);
                if (image is null) return ("Logo image not found in your library", null);
                return (null, image.Url);
            }
            return (null, null);
        }

        if (imageId.HasValue)
        {
            var owned = await _db.UserImages.AnyAsync(i => i.Id == imageId.Value && i.UserId == userId);
            if (!owned) return ("Logo image not found in your library", null);
        }
        return (null, logoUrl.Trim());
    }

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
        Specialties = c.Specialties,
        LogoImageId = c.LogoImageId,
        LogoUrl = c.LogoUrl,
        Note = c.Note,
        Description = c.Description,
        Address = c.Address,
        Emails = CompanyResearchJson.ParseList(c.EmailsJson),
        Phones = CompanyResearchJson.ParseList(c.PhonesJson),
        SocialLinks = CompanyResearchJson.ParseSocialLinks(c.SocialLinksJson),
        CompanyFacts = CompanyResearchJson.ParseList(c.CompanyFactsJson),
        ResearchSource = c.ResearchSource,
        ResearchLink = c.ResearchLink,
        ResearchUpdatedAt = c.ResearchUpdatedAt,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };

    private static CompanyCredentialDto MapCredential(CompanyCredential x) => new()
    {
        Id = x.Id,
        CompanyId = x.CompanyId,
        Label = x.Label,
        Url = x.Url,
        Username = x.Username,
        Password = x.Password,
        Notes = x.Notes,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };
}
