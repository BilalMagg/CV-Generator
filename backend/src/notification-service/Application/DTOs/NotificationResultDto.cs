using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public class NotificationResultDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}