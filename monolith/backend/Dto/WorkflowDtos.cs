namespace CV_Generator.Dto;

public class CreateWorkflowDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DefinitionJson { get; set; }
}
