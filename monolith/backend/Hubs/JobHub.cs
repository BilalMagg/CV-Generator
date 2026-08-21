using Microsoft.AspNetCore.SignalR;

namespace CV_Generator.Hubs;

public class JobHub : Hub
{
    private readonly ILogger<JobHub> _logger;

    public JobHub(ILogger<JobHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinSearchGroup(Guid searchId)
    {
        var groupName = searchId.ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} joined search group {SearchId}", Context.ConnectionId, searchId);
    }

    public async Task LeaveSearchGroup(Guid searchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, searchId.ToString());
    }
}
