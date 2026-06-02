using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers;

[Route("api/reminders")]
public class RemindersController : BaseApiController
{
    private readonly IReminderService _reminderSvc;

    public RemindersController(IReminderService reminderSvc)
    {
        _reminderSvc = reminderSvc;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReminderDto dto)
    {
        dto.UserId = GetUserId();
        var id = await _reminderSvc.CreateReminderAsync(dto);
        return CreatedAtAction(nameof(GetUserReminders), new { },
            new { id, message = "Reminder created successfully" });
    }

    [HttpGet]
    public async Task<IActionResult> GetUserReminders()
    {
        var userId = GetUserId();
        var reminders = await _reminderSvc.GetUserRemindersAsync(userId);
        return Ok(reminders);
    }

    [HttpDelete("{reminderId}")]
    public async Task<IActionResult> Cancel(Guid reminderId)
    {
        await _reminderSvc.CancelReminderAsync(reminderId);
        return NoContent();
    }
}
