using System.Text.Json;

namespace CV_Generator.Dto;

public class AutofillFieldDto
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public List<string> Options { get; set; } = new();
    public string Help { get; set; } = string.Empty;
}

public class AutofillRequestDto
{
    public string EntityType { get; set; } = "generic";
    public string Text { get; set; } = string.Empty;
    public List<AutofillFieldDto> Fields { get; set; } = new();
    public string Language { get; set; } = "English";
    public string? Provider { get; set; }
    public string? Model { get; set; }
}

public class AutofillResultDto
{
    /// <summary>Flat field-name -> JSON value map produced by the agent.</summary>
    public Dictionary<string, JsonElement> Values { get; set; } = new();
}
