using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

public class ContactService : IContactService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        AppDbContext db,
        ILogger<ContactService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ContactListResponse> GetContactsAsync(
        Guid userId, string? search, string? source, bool? favorite, int page, int pageSize)
    {
        var query = _db.Set<Contact>().Where(c => c.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.Email.ToLower().Contains(term) ||
                (c.Company != null && c.Company.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(source))
            query = query.Where(c => c.Source == source);

        if (favorite == true)
            query = query.Where(c => c.IsFavorite);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ContactDto
            {
                Id = c.Id,
                UserId = c.UserId,
                Name = c.Name,
                Email = c.Email,
                Phone = c.Phone,
                Company = c.Company,
                Position = c.Position,
                Notes = c.Notes,
                Source = c.Source,
                IsFavorite = c.IsFavorite,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return new ContactListResponse { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<ContactDto?> GetContactAsync(Guid id, Guid userId)
    {
        var c = await _db.Set<Contact>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (c is null) return null;
        return Map(c);
    }

    public async Task<ContactDto> CreateContactAsync(Guid userId, CreateContactDto dto)
    {
        ValidateContact(dto.Name, dto.Email);

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = dto.Phone,
            Mobile = dto.Mobile,
            Fax = dto.Fax,
            Address = dto.Address,
            Company = dto.Company,
            Position = dto.Position,
            Notes = dto.Notes,
            AvatarBase64 = dto.AvatarBase64,
            Source = dto.Source ?? "manual"
        };
        _db.Set<Contact>().Add(contact);
        await _db.SaveChangesAsync();
        return Map(contact);
    }

    public async Task<ContactDto?> UpdateContactAsync(Guid id, Guid userId, UpdateContactDto dto)
    {
        var c = await _db.Set<Contact>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (c is null) return null;

        if (dto.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Name cannot be empty");
            c.Name = dto.Name.Trim();
        }
        if (dto.Email is not null)
        {
            ValidateEmail(dto.Email);
            c.Email = dto.Email.Trim().ToLower();
        }
        if (dto.Phone is not null) c.Phone = dto.Phone;
        if (dto.Mobile is not null) c.Mobile = dto.Mobile;
        if (dto.Fax is not null) c.Fax = dto.Fax;
        if (dto.Address is not null) c.Address = dto.Address;
        if (dto.Company is not null) c.Company = dto.Company;
        if (dto.Position is not null) c.Position = dto.Position;
        if (dto.Notes is not null) c.Notes = dto.Notes;
        if (dto.IsFavorite.HasValue) c.IsFavorite = dto.IsFavorite.Value;
        if (dto.AvatarBase64 is not null) c.AvatarBase64 = dto.AvatarBase64;
        c.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Map(c);
    }

    public async Task<bool> DeleteContactAsync(Guid id, Guid userId)
    {
        var c = await _db.Set<Contact>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (c is null) return false;
        _db.Set<Contact>().Remove(c);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ContactDto?> ToggleFavoriteAsync(Guid id, Guid userId)
    {
        var c = await _db.Set<Contact>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (c is null) return null;
        c.IsFavorite = !c.IsFavorite;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(c);
    }

    /// <summary>
    /// Contacts whose company matches (normalized) the given name; favorites first, then by name.
    /// </summary>
    public async Task<List<ContactDto>> GetByCompanyAsync(Guid userId, string companyName)
    {
        var key = companyName.Trim().ToLower();

        var contacts = await _db.Set<Contact>().AsNoTracking()
            .Where(c => c.UserId == userId && c.Company != null && c.Company.Trim().ToLower() == key)
            .OrderByDescending(c => c.IsFavorite)
            .ThenBy(c => c.Name)
            .ToListAsync();

        return contacts.Select(Map).ToList();
    }

    public async Task<int> ImportCsvAsync(Guid userId, string csvContent)
    {
        var rows = ParseCsv(csvContent);
        if (rows.Count < 2) return 0;

        var headers = rows[0].Select(h => h.Trim().ToLower()).ToList();
        var nameIdx = headers.IndexOf("name");
        var emailIdx = headers.IndexOf("email");
        var companyIdx = headers.IndexOf("company");
        var positionIdx = headers.IndexOf("position");
        var phoneIdx = headers.IndexOf("phone");
        var mobileIdx = headers.IndexOf("mobile");
        var faxIdx = headers.IndexOf("fax");
        var addressIdx = headers.IndexOf("address");

        if (nameIdx < 0) return 0;

        // Dedup by email when present; phone-only rows dedup on company|name|phone.
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in _db.Set<Contact>().Where(c => c.UserId == userId))
        {
            if (!string.IsNullOrWhiteSpace(c.Email)) seenKeys.Add("email:" + c.Email.Trim().ToLower());
            else if (!string.IsNullOrWhiteSpace(c.Company) || !string.IsNullOrWhiteSpace(c.Name))
                seenKeys.Add("by:" + (c.Company ?? "").Trim().ToLower() + "|" + c.Name.Trim().ToLower() + "|" + (c.Phone ?? ""));
        }

        var contacts = new List<Contact>();
        foreach (var cols in rows.Skip(1))
        {
            if (cols.Count <= nameIdx) continue;
            var name = cols[nameIdx].Trim();
            var email = emailIdx >= 0 && cols.Count > emailIdx ? cols[emailIdx].Trim().ToLower() : "";
            var phone = phoneIdx >= 0 && cols.Count > phoneIdx ? cols[phoneIdx].Trim() : "";
            if (name.Length == 0 || (email.Length == 0 && phone.Length == 0)) continue;
            if (email.Length > 0)
            {
                var at = email.IndexOf('@');
                if (at < 1 || at == email.Length - 1 || !email[(at + 1)..].Contains('.')) continue;
            }

            var key = email.Length > 0
                ? "email:" + email
                : "by:" + (companyIdx >= 0 && cols.Count > companyIdx ? cols[companyIdx] : "").Trim().ToLower() + "|" + name.ToLower() + "|" + phone;
            if (!seenKeys.Add(key)) continue;

            contacts.Add(new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = name,
                Email = email,
                Phone = phone.Length > 0 ? phone : null,
                Mobile = mobileIdx >= 0 && cols.Count > mobileIdx ? cols[mobileIdx].Trim() : null,
                Fax = faxIdx >= 0 && cols.Count > faxIdx ? cols[faxIdx].Trim() : null,
                Address = addressIdx >= 0 && cols.Count > addressIdx ? cols[addressIdx].Trim() : null,
                Company = companyIdx >= 0 && cols.Count > companyIdx ? cols[companyIdx].Trim() : null,
                Position = positionIdx >= 0 && cols.Count > positionIdx ? cols[positionIdx].Trim() : null,
                Source = "csv"
            });
        }

        if (contacts.Count == 0) return 0;

        _db.Set<Contact>().AddRange(contacts);
        await _db.SaveChangesAsync();
        return contacts.Count;
    }

    public async Task<int> ImportFromJobOffersAsync(Guid userId)
    {
        try
        {
            // Local job_offers table — companies the candidate actually tracked.
            var companies = await _db.JobOffers.AsNoTracking()
                .Where(j => j.UserId == userId)
                .GroupBy(j => j.EnterpriseName.Trim().ToLower())
                .Select(g => g.First().EnterpriseName.Trim())
                .ToListAsync();
            if (companies.Count == 0) return 0;

            var existingKeys = (await _db.Set<Contact>()
                .Where(c => c.UserId == userId)
                .Select(c => new { c.Email, c.Source })
                .ToListAsync())
                .Select(c => (c.Email.ToLower(), c.Source))
                .ToHashSet();

            var contacts = new List<Contact>();
            foreach (var company in companies)
            {
                var slug = company.Replace(" ", "").Replace("-", "").ToLower();
                var email = $"recruiter@{slug}.com";

                // One placeholder per company; skip if already imported from offers.
                if (!existingKeys.Add((email, "offer-placeholder"))) continue;

                contacts.Add(new Contact
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = $"Recruiter at {company}",
                    Email = email,
                    Company = company,
                    Position = "Recruiter",
                    Notes = "Placeholder created from a tracked job offer — replace with the real person once identified.",
                    Source = "offer-placeholder"
                });
            }

            if (contacts.Count > 0)
            {
                _db.Set<Contact>().AddRange(contacts);
                await _db.SaveChangesAsync();
            }

            return contacts.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to import contacts from job offers for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<long> GetContactCountAsync(Guid userId)
    {
        return await _db.Set<Contact>().LongCountAsync(c => c.UserId == userId);
    }

    private static ContactDto Map(Contact c) => new()
    {
        Id = c.Id, UserId = c.UserId, Name = c.Name, Email = c.Email, Phone = c.Phone,
        Mobile = c.Mobile, Fax = c.Fax, Address = c.Address,
        Company = c.Company, Position = c.Position, Notes = c.Notes,
        Source = c.Source, IsFavorite = c.IsFavorite, AvatarBase64 = c.AvatarBase64,
        CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
    };

    private static void ValidateContact(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Contact name is required");
        ValidateEmail(email);
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Contact email is required");
        // Placeholder addresses (recruiter@company.com) and real ones both pass;
        // anything without a basic local@domain.tld shape is rejected.
        var at = email.IndexOf('@');
        if (at < 1 || at == email.Length - 1 || !email[(at + 1)..].Contains('.'))
            throw new ArgumentException($"Invalid email address '{email}'");
    }

    /// <summary>RFC4180-style CSV parsing: quoted fields, escaped quotes, CRLF/LF.</summary>
    private static List<List<string>> ParseCsv(string csvContent)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void EndField() { row.Add(field.ToString().Trim()); field.Clear(); }
        void EndRow()
        {
            EndField();
            if (row.Any(v => v.Length > 0)) rows.Add(row);
            row = new List<string>();
        }

        for (var i = 0; i < csvContent.Length; i++)
        {
            var ch = csvContent[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < csvContent.Length && csvContent[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == ',') EndField();
            else if (ch is '\n' or '\r')
            {
                if (ch == '\r' && i + 1 < csvContent.Length && csvContent[i + 1] == '\n') i++;
                EndRow();
            }
            else field.Append(ch);
        }
        if (field.Length > 0 || row.Count > 0) EndRow();

        return rows;
    }
}
