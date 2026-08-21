using System.Text.Json.Serialization;

namespace CV_Generator.Dto;

public class StepStatusDto
{
    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class CvGenerationStatusResponse
{
    [JsonPropertyName("run_id")]
    public Guid RunId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("current_step")]
    public int CurrentStep { get; set; }

    [JsonPropertyName("steps")]
    public List<StepStatusDto> Steps { get; set; } = new();

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("cancelled_at")]
    public DateTime? CancelledAt { get; set; }
}

public class CvGenerationResultResponse
{
    [JsonPropertyName("run_id")]
    public Guid RunId { get; set; }

    [JsonPropertyName("extraction")]
    public object? Extraction { get; set; }

    [JsonPropertyName("search")]
    public object? Search { get; set; }

    [JsonPropertyName("optimization")]
    public object? Optimization { get; set; }

    [JsonPropertyName("render")]
    public object? Render { get; set; }

    [JsonPropertyName("delivery")]
    public object? Delivery { get; set; }
}

public class CvGenerationSubmitResponse
{
    [JsonPropertyName("run_id")]
    public Guid RunId { get; set; }
}
