using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationSvc;

    public NotificationsController(INotificationService notificationSvc)
    {
        _notificationSvc = notificationSvc;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserNotifications(Guid userId)
    {
        var notifications = await _notificationSvc.GetUserNotificationsAsync(userId);
        return Ok(notifications);
    }

    [HttpPatch("{notificationId}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        await _notificationSvc.MarkAsReadAsync(notificationId);
        return NoContent();
    }

    [HttpPost("test/welcome")]
    public async Task<IActionResult> TestWelcome([FromBody] TestWelcomeRequest req)
    {
        await _notificationSvc.SendWelcomeAsync(req.UserId, req.Email, req.FirstName);
        return Ok(new { message = "Welcome email triggered" });
    }

    [HttpGet("{userId}/preferences")]
    public async Task<IActionResult> GetPreferences(Guid userId)
    {
        var prefs = await _notificationSvc.GetUserPreferencesAsync(userId);
        return Ok(prefs);
    }

    [HttpPut("{userId}/preferences")]
    public async Task<IActionResult> UpdatePreferences(Guid userId, [FromBody] UpdateNotificationPreferenceDto dto)
    {
        var prefs = await _notificationSvc.UpdateUserPreferencesAsync(userId, dto);
        return Ok(prefs);
    }
}

public record TestWelcomeRequest(Guid UserId, string Email, string FirstName);