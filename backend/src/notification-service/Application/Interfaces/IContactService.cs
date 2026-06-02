using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface IContactService
{
    Task<ContactListResponse> GetContactsAsync(Guid userId, string? search, string? source, bool? favorite, int page, int pageSize);
    Task<ContactDto?> GetContactAsync(Guid id, Guid userId);
    Task<ContactDto> CreateContactAsync(Guid userId, CreateContactDto dto);
    Task<ContactDto?> UpdateContactAsync(Guid id, Guid userId, UpdateContactDto dto);
    Task<bool> DeleteContactAsync(Guid id, Guid userId);
    Task<int> ImportCsvAsync(Guid userId, string csvContent);
    Task<int> ImportFromJobOffersAsync(Guid userId);
    Task<long> GetContactCountAsync(Guid userId);
    Task<ContactDto?> ToggleFavoriteAsync(Guid id, Guid userId);
}
