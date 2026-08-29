using System.Text.Json.Serialization;

namespace CV_Generator.Dto;

public class TemplateRenderRequest
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("extraction_id")]
    public Guid ExtractionId { get; set; }

    [JsonPropertyName("template_id")]
    public string? TemplateId { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("tone")]
    public string? Tone { get; set; }

    [JsonPropertyName("save_to_documents")]
    public bool SaveToDocuments { get; set; } = true;

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

public class TemplateRenderSubmitResponse
{
    [JsonPropertyName("run_id")]
    public Guid RunId { get; set; }
}

public class TemplateRenderStatusResponse
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

public class TemplateRenderResultResponse
{
    [JsonPropertyName("run_id")]
    public Guid RunId { get; set; }

    [JsonPropertyName("extraction")]
    public object? Extraction { get; set; }

    [JsonPropertyName("search")]
    public object? Search { get; set; }

    [JsonPropertyName("render")]
    public object? Render { get; set; }

    [JsonPropertyName("tex")]
    public string Tex { get; set; } = string.Empty;

    [JsonPropertyName("pdf_url")]
    public string? PdfUrl { get; set; }

    [JsonPropertyName("cv_id")]
    public Guid? CvId { get; set; }

    [JsonPropertyName("cv_version_id")]
    public Guid? CvVersionId { get; set; }

    [JsonPropertyName("saved")]
    public bool Saved { get; set; }

    [JsonPropertyName("match_score")]
    public double? MatchScore { get; set; }

    [JsonPropertyName("gap_skills")]
    public List<string> GapSkills { get; set; } = new();
}

// Server-Sent Events pushed by GET /events while a run executes.
public class TemplateRenderSseEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("current_step")]
    public int? CurrentStep { get; set; }

    [JsonPropertyName("steps")]
    public List<StepStatusDto>? Steps { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("result")]
    public TemplateRenderResultResponse? Result { get; set; }
}