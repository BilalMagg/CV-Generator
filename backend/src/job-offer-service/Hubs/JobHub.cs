using Microsoft.AspNetCore.SignalR;

namespace JobOfferService.Hubs;

/// <summary>
/// SignalR hub for real-time job crawl updates.
///
/// The frontend connects and calls JoinSearchGroup(searchId) to subscribe
/// to a specific crawl session. The server then pushes:
///   - "JobArrived"     → each time a job is processed and saved
///   - "SearchFinished" → when ProcessedCount >= ExpectedCount
/// </summary>
public class JobHub : Hub
{
    private readonly ILogger<JobHub> _logger;

    public JobHub(ILogger<JobHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called by the frontend immediately after connecting.
    /// Groups are named after the SearchId GUID (e.g. "11111111-...").
    /// </summary>
    public async Task JoinSearchGroup(Guid searchId)
    {
        var groupName = searchId.ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "Client {ConnectionId} joined search group {SearchId}",
            Context.ConnectionId, searchId);
    }

    /// <summary>Optional: allows the frontend to leave a group explicitly.</summary>
    public async Task LeaveSearchGroup(Guid searchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, searchId.ToString());
    }
}
