using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/reminders")]
public class RemindersController : ControllerBase
{
    private readonly IReminderService _reminderSvc;

    public RemindersController(IReminderService reminderSvc)
    {
        _reminderSvc = reminderSvc;
    }

    /// <summary>Create a new reminder</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReminderDto dto)
    {
        var id = await _reminderSvc.CreateReminderAsync(dto);
        return CreatedAtAction(nameof(GetUserReminders), new { userId = dto.UserId },
            new { id, message = "Reminder created successfully" });
    }

    /// <summary>Get all reminders for a user</summary>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserReminders(Guid userId)
    {
        var reminders = await _reminderSvc.GetUserRemindersAsync(userId);
        return Ok(reminders);
    }

    /// <summary>Cancel a pending reminder</summary>
    [HttpDelete("{reminderId}")]
    public async Task<IActionResult> Cancel(Guid reminderId)
    {
        await _reminderSvc.CancelReminderAsync(reminderId);
        return NoContent();
    }
}
