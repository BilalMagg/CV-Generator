using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkflowService.Models;

// Generic JobRequirements used by JobExtractor and SearchAgent
public class JobRequirements
{
    [JsonPropertyName("job_role")]
    public string? JobRole { get; set; }

    [JsonPropertyName("extracted_skills")]
    public List<string> ExtractedSkills { get; set; } = new();

    [JsonPropertyName("required_experience_years")]
    public int? RequiredExperienceYears { get; set; }

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("seniority_level")]
    public string? SeniorityLevel { get; set; }

    [JsonPropertyName("employment_type")]
    public string? EmploymentType { get; set; }

    [JsonPropertyName("location_type")]
    public string? LocationType { get; set; }

    [JsonPropertyName("responsibilities")]
    public List<string> Responsibilities { get; set; } = new();

    [JsonPropertyName("certifications")]
    public List<string> Certifications { get; set; } = new();
}

// Job Extractor — sent to the Python agent
public class JobExtractorAgentRequest
{
    [JsonPropertyName("job_description")]
    public string? JobDescription { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("job_offer_id")]
    public string? JobOfferId { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";
}

// Legacy — kept for backward compat
public class ExtractorInput
{
    [JsonPropertyName("job_description")]
    public string JobDescription { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";
}

public class ExtractorOutput : JobRequirements
{
    [JsonPropertyName("enterprise_name")]
    public string? EnterpriseName { get; set; }

    [JsonPropertyName("enterprise_description")]
    public string? EnterpriseDescription { get; set; }

    [JsonPropertyName("enterprise_logo_url")]
    public string? EnterpriseLogoUrl { get; set; }

    [JsonPropertyName("raw_description")]
    public string? RawDescription { get; set; }

    [JsonPropertyName("required_skills")]
    public List<string> RequiredSkills { get; set; } = new();

    [JsonPropertyName("soft_skills")]
    public List<string> SoftSkills { get; set; } = new();

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("salary_range")]
    public string? SalaryRange { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("education_requirements")]
    public string? EducationRequirements { get; set; }

    [JsonPropertyName("benefits")]
    public List<string> Benefits { get; set; } = new();

    [JsonPropertyName("application_deadline")]
    public string? ApplicationDeadline { get; set; }

    [JsonPropertyName("contact_email")]
    public string? ContactEmail { get; set; }

    [JsonPropertyName("source_url")]
    public string? SourceUrl { get; set; }

    [JsonPropertyName("languages")]
    public List<string> Languages { get; set; } = new();

    [JsonPropertyName("overall_confidence")]
    public double OverallConfidence { get; set; }

    [JsonPropertyName("field_confidences")]
    public Dictionary<string, double>? FieldConfidences { get; set; }
}

// Search Agent
public class SearchInput
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("job_requirements")]
    public JobRequirements JobRequirements { get; set; } = new();
}

public class SearchOutput
{
    [JsonPropertyName("matched_skills")]
    public List<dynamic> MatchedSkills { get; set; } = new();

    [JsonPropertyName("matched_experiences")]
    public List<dynamic> MatchedExperiences { get; set; } = new();

    [JsonPropertyName("matched_projects")]
    public List<dynamic> MatchedProjects { get; set; } = new();

    [JsonPropertyName("gap_skills")]
    public List<string> GapSkills { get; set; } = new();

    [JsonPropertyName("match_score")]
    public double MatchScore { get; set; }
}

// CV Optimizer
public class OptimizerInput
{
    [JsonPropertyName("job_data")]
    public string JobData { get; set; } = string.Empty;

    [JsonPropertyName("candidate_name")]
    public string CandidateName { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("user_focus")]
    public string? UserFocus { get; set; }

    [JsonPropertyName("cv_content")]
    public string? CvContent { get; set; }
}

public class OptimizerOutput
{
    [JsonPropertyName("ats_score_before")]
    public int AtsScoreBefore { get; set; }

    [JsonPropertyName("ats_score_after")]
    public int AtsScoreAfter { get; set; }

    [JsonPropertyName("improvement")]
    public int Improvement { get; set; }

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;
}

// Template Agent
public class TemplateInput
{
    [JsonPropertyName("cv_draft")]
    public dynamic CvDraft { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("template_id")]
    public string TemplateId { get; set; } = "default";

    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "pdf"; // latex | html | pdf

    [JsonPropertyName("target_role")]
    public string TargetRole { get; set; } = string.Empty;
}

public class RenderedCV
{
    [JsonPropertyName("cv_code")]
    public string CvCode { get; set; } = string.Empty; // Using string to handle both text and base64 encoded bytes

    [JsonPropertyName("template_id")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("sections")]
    public List<dynamic>? Sections { get; set; }

    [JsonPropertyName("pdf_url")]
    public string? PdfUrl { get; set; }

    [JsonPropertyName("code_url")]
    public string? CodeUrl { get; set; }
}

// Request for the direct template-render endpoint (api/workflows/template/render).
// Carries user_id for context; when CvDraft is null the controller builds the
// matched profile from the workflow DB using UserId.
public class TemplateRenderRequest
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("target_role")]
    public string TargetRole { get; set; } = string.Empty;

    [JsonPropertyName("template_id")]
    public string TemplateId { get; set; } = "default";

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("cv_draft")]
    public JsonElement? CvDraft { get; set; }
}

// Contact Agent
public class ContactInput
{
    [JsonPropertyName("optimized_cv")]
    public dynamic OptimizedCv { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("job_title")]
    public string JobTitle { get; set; } = string.Empty;

    [JsonPropertyName("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("job_description")]
    public string JobDescription { get; set; } = string.Empty;

    [JsonPropertyName("recipient_email")]
    public string RecipientEmail { get; set; } = string.Empty;

    [JsonPropertyName("cover_letter_hint")]
    public string? CoverLetterHint { get; set; }
}

public class ContactOutput
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("delivery_id")]
    public string DeliveryId { get; set; } = string.Empty;

    [JsonPropertyName("sent_at")]
    public DateTime SentAt { get; set; }

    [JsonPropertyName("subject_used")]
    public string SubjectUsed { get; set; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

// Orchestrator Feature Models
public class GenerateCvRequest
{
    [JsonPropertyName("user_id")]
    public Guid UserId { get; set; }

    [JsonPropertyName("job_description")]
    public string JobDescription { get; set; } = string.Empty;

    [JsonPropertyName("candidate_name")]
    public string? CandidateName { get; set; }

    [JsonPropertyName("recipient_email")]
    public string? RecipientEmail { get; set; }

    [JsonPropertyName("template_id")]
    public string? TemplateId { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("tone")]
    public string? Tone { get; set; }

    [JsonPropertyName("email_subject")]
    public string? EmailSubject { get; set; }
}
