namespace WorkflowService.Models;

public class AgentHealthStatus
{
    public string AgentName { get; set; } = string.Empty;
    public bool Healthy { get; set; }
    public long LatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}
