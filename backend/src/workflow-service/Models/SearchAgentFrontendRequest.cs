using System.Text.Json.Serialization;

namespace WorkflowService.Models;

public class SearchAgentFrontendRequest
{
    [JsonPropertyName("extractedJobId")]
    public Guid? ExtractedJobId { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("keywords")]
    public string? Keywords { get; set; }
}
