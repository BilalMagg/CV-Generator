namespace WorkflowService.Models;

public class WorkflowDefinition
{
    public List<WorkflowStep> Steps { get; set; } = new();
}

public class WorkflowStep
{
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
}
