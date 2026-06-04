using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Application.Services;

public class ContactService : IContactService
{
    private readonly NotificationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        NotificationDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<ContactService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
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
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
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

        if (dto.Name is not null) c.Name = dto.Name;
        if (dto.Email is not null) c.Email = dto.Email;
        if (dto.Phone is not null) c.Phone = dto.Phone;
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

    public async Task<int> ImportCsvAsync(Guid userId, string csvContent)
    {
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return 0;

        var headers = lines[0].Split(',').Select(h => h.Trim().ToLower()).ToList();
        var nameIdx = headers.IndexOf("name");
        var emailIdx = headers.IndexOf("email");
        var companyIdx = headers.IndexOf("company");
        var positionIdx = headers.IndexOf("position");
        var phoneIdx = headers.IndexOf("phone");

        if (nameIdx < 0 || emailIdx < 0) return 0;

        var contacts = new List<Contact>();
        foreach (var line in lines.Skip(1))
        {
            var cols = line.Split(',').Select(c => c.Trim().Trim('"')).ToList();
            if (cols.Count <= Math.Max(nameIdx, emailIdx)) continue;

            contacts.Add(new Contact
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = cols[nameIdx],
                Email = cols[emailIdx],
                Phone = phoneIdx >= 0 && cols.Count > phoneIdx ? cols[phoneIdx] : null,
                Company = companyIdx >= 0 && cols.Count > companyIdx ? cols[companyIdx] : null,
                Position = positionIdx >= 0 && cols.Count > positionIdx ? cols[positionIdx] : null,
                Source = "csv"
            });
        }

        _db.Set<Contact>().AddRange(contacts);
        await _db.SaveChangesAsync();
        return contacts.Count;
    }

    public async Task<int> ImportFromJobOffersAsync(Guid userId)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(
                $"http://cv-job-offer-service:8086/api/job-offers?userId={userId}&page=1&pageSize=500");
            if (!response.IsSuccessStatusCode) return 0;

            var json = await response.Content.ReadAsStringAsync();
            var offers = JsonSerializer.Deserialize<JobOfferImportDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (offers?.Items is null) return 0;

            var existingEmails = await _db.Set<Contact>()
                .Where(c => c.UserId == userId)
                .Select(c => c.Email.ToLower())
                .ToListAsync();

            var contacts = new List<Contact>();
            foreach (var offer in offers.Items)
            {
                var company = offer.EnterpriseName ?? "Unknown";
                var email = $"recruiter@{company.Replace(" ", "").ToLower()}.com";

                if (existingEmails.Contains(email.ToLower())) continue;

                contacts.Add(new Contact
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = $"Recruiter at {company}",
                    Email = email,
                    Company = company,
                    Position = "Recruiter",
                    Source = "offer"
                });
                existingEmails.Add(email.ToLower());
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
        Company = c.Company, Position = c.Position, Notes = c.Notes,
        Source = c.Source, IsFavorite = c.IsFavorite, AvatarBase64 = c.AvatarBase64,
        CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
    };

    private class JobOfferImportDto
    {
        public List<JobOfferItem> Items { get; set; } = [];
    }

    private class JobOfferItem
    {
        public string? EnterpriseName { get; set; }
    }
}
