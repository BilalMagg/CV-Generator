namespace NotificationService.Application.DTOs;

public class SendNotificationDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
}