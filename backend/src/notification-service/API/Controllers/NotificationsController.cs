using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/notification")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationSvc;

    public NotificationsController(INotificationService notificationSvc)
    {
        _notificationSvc = notificationSvc;
    }

    /// <summary>Get all notifications for a user (in-app inbox)</summary>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserNotifications(Guid userId)
    {
        var notifications = await _notificationSvc.GetUserNotificationsAsync(userId);
        return Ok(notifications);
    }

    /// <summary>Mark a notification as read</summary>
    [HttpPatch("{notificationId}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        await _notificationSvc.MarkAsReadAsync(notificationId);
        return NoContent();
    }

    /// <summary>Manual trigger for testing — remove in production</summary>
    [HttpPost("test/welcome")]
    public async Task<IActionResult> TestWelcome([FromBody] TestWelcomeRequest req)
    {
        await _notificationSvc.SendWelcomeAsync(req.UserId, req.Email, req.FirstName);
        return Ok(new { message = "Welcome email triggered" });
    }
}

public record TestWelcomeRequest(Guid UserId, string Email, string FirstName);