using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Models;
using CV_Generator.Dto;

namespace CV_Generator.Services;

public class BimeService : IBimeService
{
    private readonly AppDbContext _db;

    public BimeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BimeConversationSummaryDto?> CreateConversationAsync(Guid? userId, string title)
    {
        var conv = new BimeConversation
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? Guid.Empty,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.BimeConversations.Add(conv);
        await _db.SaveChangesAsync();
        return new BimeConversationSummaryDto
        {
            Id = conv.Id,
            Title = conv.Title,
            CreatedAt = conv.CreatedAt,
            UpdatedAt = conv.UpdatedAt,
            MessageCount = 0,
        };
    }

    public async Task<List<BimeConversationSummaryDto>> ListConversationsAsync(Guid? userId)
    {
        return await _db.BimeConversations
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new BimeConversationSummaryDto
            {
                Id = c.Id,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                MessageCount = _db.BimeMessages.Count(m => m.ConversationId == c.Id),
            })
            .ToListAsync();
    }

    public async Task<BimeConversationDetailDto?> GetConversationAsync(Guid conversationId, Guid? userId)
    {
        var conv = await _db.BimeConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);
        if (conv == null) return null;

        var messages = await _db.BimeMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new BimeMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
            })
            .ToListAsync();

        return new BimeConversationDetailDto
        {
            Id = conv.Id,
            Title = conv.Title,
            Messages = messages,
        };
    }

    public async Task<BimeConversationDetailDto?> GetConversationBySharedIdAsync(string conversationId, Guid? userId)
    {
        if (Guid.TryParse(conversationId, out var guid))
            return await GetConversationAsync(guid, userId);
        return null;
    }

    public async Task AddMessagesAsync(Guid conversationId, List<BimeMessageDto> messages)
    {
        foreach (var msg in messages)
        {
            _db.BimeMessages.Add(new BimeMessage
            {
                Id = msg.Id == Guid.Empty ? Guid.NewGuid() : msg.Id,
                ConversationId = conversationId,
                Role = msg.Role,
                Content = msg.Content,
                CreatedAt = DateTime.UtcNow,
            });
        }
        var conv = await _db.BimeConversations.FindAsync(conversationId);
        if (conv != null) conv.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteConversationAsync(Guid conversationId, Guid? userId)
    {
        var conv = await _db.BimeConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);
        if (conv == null) return;

        var messages = await _db.BimeMessages
            .Where(m => m.ConversationId == conversationId)
            .ToListAsync();
        _db.BimeMessages.RemoveRange(messages);
        _db.BimeConversations.Remove(conv);
        await _db.SaveChangesAsync();
    }

    public async Task<List<BimeMessageDto>> GetMessagesAsync(Guid conversationId)
    {
        return await _db.BimeMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new BimeMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
            })
            .ToListAsync();
    }
}
