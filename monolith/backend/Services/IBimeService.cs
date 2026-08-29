using CV_Generator.Dto;

namespace CV_Generator.Services;

public interface IBimeService
{
    Task<BimeConversationSummaryDto?> CreateConversationAsync(Guid? userId, string title);
    Task<List<BimeConversationSummaryDto>> ListConversationsAsync(Guid? userId);
    Task<BimeConversationDetailDto?> GetConversationAsync(Guid conversationId, Guid? userId);
    Task<BimeConversationDetailDto?> GetConversationBySharedIdAsync(string conversationId, Guid? userId);
    Task AddMessagesAsync(Guid conversationId, List<BimeMessageDto> messages);
    Task DeleteConversationAsync(Guid conversationId, Guid? userId);
    Task<List<BimeMessageDto>> GetMessagesAsync(Guid conversationId);
}
