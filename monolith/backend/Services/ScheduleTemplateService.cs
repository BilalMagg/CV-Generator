using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

public class ScheduleTemplateService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ScheduleTemplateService> _logger;

    public ScheduleTemplateService(AppDbContext db, ILogger<ScheduleTemplateService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ScheduleTemplateDto>> GetAllAsync(Guid userId)
    {
        return await _db.ScheduleTemplates
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => Map(t))
            .ToListAsync();
    }

    public async Task<ScheduleTemplateDto?> GetAsync(Guid id, Guid userId)
    {
        var t = await _db.ScheduleTemplates.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        return t is null ? null : Map(t);
    }

    public async Task<ScheduleTemplateDto> CreateAsync(Guid userId, CreateScheduleTemplateDto dto)
    {
        var template = new ScheduleTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name.Trim(),
            SubjectTemplate = dto.SubjectTemplate,
            BodyTemplate = dto.BodyTemplate,
            VariableDefaultsJson = dto.VariableDefaults is null ? null : JsonSerializer.Serialize(dto.VariableDefaults),
            CvVersionId = dto.CvVersionId,
            AttachmentRefsJson = dto.Attachments is { Count: > 0 }
                ? JsonSerializer.Serialize(dto.Attachments)
                : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ScheduleTemplates.Add(template);
        await _db.SaveChangesAsync();
        return Map(template);
    }

    public async Task<ScheduleTemplateDto?> UpdateAsync(Guid id, Guid userId, UpdateScheduleTemplateDto dto)
    {
        var t = await _db.ScheduleTemplates.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (t is null) return null;

        if (dto.Name is not null) t.Name = dto.Name.Trim();
        if (dto.SubjectTemplate is not null) t.SubjectTemplate = dto.SubjectTemplate;
        if (dto.BodyTemplate is not null) t.BodyTemplate = dto.BodyTemplate;
        if (dto.VariableDefaults is not null)
            t.VariableDefaultsJson = JsonSerializer.Serialize(dto.VariableDefaults);
        if (dto.CvVersionId.HasValue) t.CvVersionId = dto.CvVersionId;
        if (dto.Attachments is not null)
            t.AttachmentRefsJson = dto.Attachments.Count > 0
                ? JsonSerializer.Serialize(dto.Attachments)
                : null;
        t.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Map(t);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var t = await _db.ScheduleTemplates.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (t is null) return false;
        _db.ScheduleTemplates.Remove(t);
        await _db.SaveChangesAsync();
        return true;
    }

    private static ScheduleTemplateDto Map(ScheduleTemplate t) => new()
    {
        Id = t.Id,
        UserId = t.UserId,
        Name = t.Name,
        SubjectTemplate = t.SubjectTemplate,
        BodyTemplate = t.BodyTemplate,
        VariableDefaults = string.IsNullOrWhiteSpace(t.VariableDefaultsJson)
            ? null
            : JsonSerializer.Deserialize<ScheduleVariableDefaults>(t.VariableDefaultsJson),
        CvVersionId = t.CvVersionId,
        Attachments = string.IsNullOrWhiteSpace(t.AttachmentRefsJson)
            ? []
            : JsonSerializer.Deserialize<List<ScheduleAttachmentRef>>(t.AttachmentRefsJson) ?? [],
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}