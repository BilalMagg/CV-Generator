using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CV_Generator;
using CV_Generator.Data;
using CV_Generator.Models;
using CV_Generator.Services;

namespace CV_Generator.Controllers;

public class CompanyImportRow
{
    public string? Name { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Location { get; set; }
    public string? Country { get; set; }
    public string? LocationUrl { get; set; }
    public string? Note { get; set; }
}

public class ContactImportRow
{
    public string? Company { get; set; }
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class ApplicationImportRow
{
    public string? ExternalId { get; set; }      // e.g. APP-001 from the sheet
    public string? Company { get; set; }
    public string? Position { get; set; }
    public string? InternshipType { get; set; }
    public string? SourceType { get; set; }
    public string? SourceName { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? ApplyDate { get; set; }       // DD/MM/YYYY (day-first)
    public string? CvUrl { get; set; }
    public bool MotivationLetterSent { get; set; }
    public bool PortfolioSent { get; set; }
    public string? ShouldApplyAgain { get; set; }
    public string? Notes { get; set; }
}

public class ImportResultDto
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = [];
}

[Route("api/imports")]
public class ImportsController : BaseApiController
{
    private readonly AppDbContext _db;
    private readonly ILogger<ImportsController> _logger;

    public ImportsController(ICurrentUserService currentUser, AppDbContext db, ILogger<ImportsController> logger)
        : base(currentUser)
    {
        _db = db;
        _logger = logger;
    }

    // ── Companies ────────────────────────────────────────────────────────────
    [HttpPost("companies")]
    public async Task<IActionResult> ImportCompanies([FromBody] List<CompanyImportRow> rows)
    {
        var userId = GetUserId();
        if (rows is null || rows.Count == 0)
            return BadRequest(ApiResponse<ImportResultDto>.Error("No rows provided"));

        var result = new ImportResultDto();
        var existing = await _db.Companies.Where(c => c.UserId == userId).ToListAsync();
        var byName = existing.ToDictionary(c => Normalize(c.Name), c => c);

        foreach (var (row, idx) in rows.Select((r, i) => (r, i)))
        {
            try
            {
                var name = row.Name?.Trim() ?? "";
                if (name.Length == 0) { result.Skipped++; continue; }

                var key = Normalize(name);
                if (byName.TryGetValue(key, out var dup))
                {
                    // Enrich the existing entry with any new info.
                    dup.WebsiteUrl ??= row.WebsiteUrl;
                    dup.Location ??= row.Location;
                    dup.LocationUrl ??= row.LocationUrl;
                    dup.Note ??= row.Note;
                    result.Skipped++;
                    continue;
                }

                var company = new Company
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = name,
                    WebsiteUrl = row.WebsiteUrl,
                    Location = row.Location,
                    Country = string.IsNullOrWhiteSpace(row.Country) ? "Morocco" : row.Country.Trim(),
                    LocationUrl = row.LocationUrl,
                    Note = row.Note
                };
                _db.Companies.Add(company);
                byName[key] = company;
                result.Imported++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Company import row {Index} failed", idx);
                result.Errors.Add($"Row {idx + 1}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<ImportResultDto>.Ok(result, $"{result.Imported} imported, {result.Skipped} skipped"));
    }

    // ── Contacts ─────────────────────────────────────────────────────────────
    [HttpPost("contacts")]
    public async Task<IActionResult> ImportContacts([FromBody] List<ContactImportRow> rows)
    {
        var userId = GetUserId();
        if (rows is null || rows.Count == 0)
            return BadRequest(ApiResponse<ImportResultDto>.Error("No rows provided"));

        var result = new ImportResultDto();
        var existingEmails = (await _db.Contacts.Where(c => c.UserId == userId).Select(c => c.Email).ToListAsync())
            .Select(e => e.ToLower()).ToHashSet();

        foreach (var (row, idx) in rows.Select((r, i) => (r, i)))
        {
            try
            {
                var name = row.Name?.Trim() ?? "";
                var email = row.Email?.Trim().ToLower() ?? "";
                if (name.Length == 0 || email.Length == 0 || !IsValidEmail(email))
                {
                    result.Skipped++;
                    if (name.Length > 0 && email.Length > 0 && !IsValidEmail(email))
                        result.Errors.Add($"Row {idx + 1}: invalid email '{email}' for '{name}'");
                    continue;
                }
                if (!existingEmails.Add(email)) { result.Skipped++; continue; }

                _db.Contacts.Add(new Contact
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = name,
                    Email = email,
                    Phone = row.Phone,
                    Company = row.Company,
                    Position = row.Role,
                    Source = "import"
                });
                result.Imported++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Contact import row {Index} failed", idx);
                result.Errors.Add($"Row {idx + 1}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<ImportResultDto>.Ok(result, $"{result.Imported} imported, {result.Skipped} skipped"));
    }

    // ── Applications ─────────────────────────────────────────────────────────
    [HttpPost("applications")]
    public async Task<IActionResult> ImportApplications([FromBody] List<ApplicationImportRow> rows)
    {
        var userId = GetUserId();
        if (rows is null || rows.Count == 0)
            return BadRequest(ApiResponse<ImportResultDto>.Error("No rows provided"));

        var result = new ImportResultDto();
        var contacts = await _db.Contacts.AsNoTracking().Where(c => c.UserId == userId).ToListAsync();

        foreach (var (row, idx) in rows.Select((r, i) => (r, i)))
        {
            try
            {
                var company = row.Company?.Trim() ?? "";
                var position = row.Position?.Trim() ?? "";
                if (company.Length == 0)
                {
                    result.Skipped++;
                    continue;
                }

                var appliedAt = ParseDate(row.ApplyDate);
                var status = ApplicationStatus.SAVED;
                if (appliedAt.HasValue && !string.IsNullOrWhiteSpace(row.Status))
                {
                    status = ParseStatus(row.Status);
                }
                else if (appliedAt.HasValue)
                {
                    status = ApplicationStatus.APPLIED;
                }
                else if (!string.IsNullOrWhiteSpace(row.Status) &&
                         Enum.TryParse<ApplicationStatus>(NormalizeEnum(row.Status), out var parsed))
                {
                    status = parsed;
                }

                var priority = ApplicationPriority.MEDIUM;
                if (!string.IsNullOrWhiteSpace(row.Priority) &&
                    Enum.TryParse<ApplicationPriority>(NormalizeEnum(row.Priority), out var p))
                {
                    priority = p;
                }

                var contactId = FindContact(contacts, company, row.SourceName);
                var composedNotes = ComposeNotes(row);

                // Intentionally bypasses the fingerprint dedup: sheet rows are distinct outreach even when
                // company+position repeat. ExternalId goes into notes for traceability.
                var app = new Application
                {
                    Id = Guid.NewGuid(),
                    CandidateId = userId,
                    CompanyName = company,
                    PositionTitle = position.Length == 0 ? "—" : position,
                    OfferSource = BuildOfferSource(row.SourceType),
                    Notes = composedNotes,
                    Status = status,
                    AppliedAt = appliedAt,
                    Priority = priority,
                    InternshipType = Truncate(row.InternshipType?.Trim(), 50)
                };
                app.Fingerprint = FingerprintHelper.Compute(company, app.PositionTitle);
                _db.Applications.Add(app);

                if (status != ApplicationStatus.SAVED)
                {
                    _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                    {
                        ApplicationId = app.Id,
                        OldStatus = null,
                        NewStatus = status,
                        Comment = "Imported from tracking sheet",
                        ChangedAt = appliedAt ?? DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
                result.Imported++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Application import row {Index} failed", idx);
                result.Errors.Add($"{row.ExternalId ?? $"Row {idx + 1}"}: {ex.Message}");
            }
        }

        return Ok(ApiResponse<ImportResultDto>.Ok(result, $"{result.Imported} imported, {result.Skipped} skipped"));
    }

    // ── Generic user-content (My Career) ─────────────────────────────────────
    private static readonly Dictionary<string, Type> UserContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cvprofiles"] = typeof(CVProfile),
        ["projects"] = typeof(Project),
        ["skills"] = typeof(Skill),
        ["experiences"] = typeof(Experience),
        ["educations"] = typeof(Education),
        ["certifications"] = typeof(Certification),
        ["languages"] = typeof(Language),
        ["interests"] = typeof(Interest),
        ["sociallinks"] = typeof(SocialLink),
        ["academicactivities"] = typeof(AcademicActivity),
        ["hackathons"] = typeof(Hackathon),
    };

    [HttpPost("{entity}")]
    public async Task<IActionResult> ImportUserContent(string entity, [FromBody] List<Dictionary<string, string>> rows)
    {
        var userId = GetUserId();
        if (rows is null || rows.Count == 0)
            return BadRequest(ApiResponse<ImportResultDto>.Error("No rows provided"));

        if (!UserContentTypes.TryGetValue(entity, out var type))
            return BadRequest(ApiResponse<ImportResultDto>.Error($"Unknown import type '{entity}'"));

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);

        var result = new ImportResultDto();
        foreach (var (row, idx) in rows.Select((r, i) => (r, i)))
        {
            try
            {
                var instance = Activator.CreateInstance(type)!;
                foreach (var kv in row)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                    var key = kv.Key.ToLowerInvariant();
                    if (key is "id" or "userid") continue;
                    if (!props.TryGetValue(key, out var prop)) continue;
                    var val = ConvertValue(prop.PropertyType, kv.Value!);
                    if (val is not null) prop.SetValue(instance, val);
                }

                type.GetProperty("Id")!.SetValue(instance, Guid.NewGuid());
                type.GetProperty("UserId")!.SetValue(instance, userId);
                _db.Add(instance);
                await _db.SaveChangesAsync();
                result.Imported++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "User-content import row {Index} failed", idx);
                result.Errors.Add($"Row {idx + 1}: {ex.Message}");
            }
        }

        return Ok(ApiResponse<ImportResultDto>.Ok(result, $"{result.Imported} imported, {result.Skipped} skipped"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();

    private static string NormalizeEnum(string s) =>
        s.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "_");

    private static bool IsValidEmail(string email) =>
        email.Contains('@') && email.Contains('.') && !email.Contains(' ') && email.Length >= 5;

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s! : (s.Length <= max ? s : s[..max]);

    /// Coerces a raw sheet cell into the target property type (strings pass through; numbers/dates/enums parsed).
    private static object? ConvertValue(Type target, string raw)
    {
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        if (underlying == typeof(string)) return raw;
        if (underlying == typeof(Guid)) return null; // ids are generated server-side
        if (underlying == typeof(int)) return int.TryParse(raw, out var i) ? i : null;
        if (underlying == typeof(double)) return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (underlying == typeof(bool)) return bool.TryParse(raw, out var b) ? b : null;
        if (underlying == typeof(DateTime)) return ParseDate(raw);
        if (underlying.IsEnum) return Enum.TryParse(underlying, raw, true, out var e) ? e : null;
        return raw;
    }

    /// Parses day-first dates as used in the tracking sheets (e.g. 04/05/2026 = May 4th).
    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd/MM/yyyy HH:mm" };
        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToUniversalTime();
        return null;
    }

    private static ApplicationStatus ParseStatus(string s) => s.Trim().ToLowerInvariant() switch
    {
        "saved" or "to apply" or "should i apply again" or "yes" => ApplicationStatus.SAVED,
        "applied" or "application sent" or "sent" => ApplicationStatus.APPLIED,
        "screening" or "hr screen" or "phone screen" => ApplicationStatus.SCREENING,
        "interview" or "interviewing" => ApplicationStatus.INTERVIEW,
        "offer" => ApplicationStatus.OFFER,
        "accepted" or "hired" => ApplicationStatus.ACCEPTED,
        "rejected" or "refused" => ApplicationStatus.REJECTED,
        "withdrawn" => ApplicationStatus.WITHDRAWN,
        _ => ApplicationStatus.SAVED
    };

    private static string? BuildOfferSource(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType)) return null;
        return sourceType.Trim().ToLowerInvariant() switch
        {
            "person" => "Referral",
            "linkedin" => "LinkedIn",
            "website" or "site" => "Company website",
            _ => sourceType.Trim()
        };
    }

    private static Guid? FindContact(List<Contact> contacts, string company, string? contactName)
    {
        if (string.IsNullOrWhiteSpace(contactName)) return null;
        var nameKey = Normalize(contactName);
        var companyKey = Normalize(company);

        var exact = contacts.FirstOrDefault(c =>
            Normalize(c.Name) == nameKey && c.Company != null && Normalize(c.Company) == companyKey);
        if (exact != null) return exact.Id;

        var fuzzy = contacts.FirstOrDefault(c =>
            Normalize(c.Name) == nameKey ||
            (c.Company != null && Normalize(c.Company) == companyKey && Normalize(c.Name).Contains(nameKey)));
        return fuzzy?.Id;
    }

    private static string? ComposeNotes(ApplicationImportRow row)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(row.ExternalId)) sb.AppendLine($"Sheet ref: {row.ExternalId.Trim()}");
        if (!string.IsNullOrWhiteSpace(row.Notes)) sb.AppendLine(row.Notes.Trim());
        if (!string.IsNullOrWhiteSpace(row.CvUrl)) sb.AppendLine($"CV: {row.CvUrl.Trim()}");
        if (row.MotivationLetterSent) sb.AppendLine("Motivation letter: sent");
        if (row.PortfolioSent) sb.AppendLine("Portfolio: sent");
        if (!string.IsNullOrWhiteSpace(row.ShouldApplyAgain) && !row.ShouldApplyAgain.Equals("No", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"Should apply again: {row.ShouldApplyAgain.Trim()}");
        var text = sb.ToString().TrimEnd();
        return text.Length == 0 ? null : text;
    }
}
