namespace CV_Generator.Dto;

public class DirectMessageRequestDto
{
    public string Channel { get; set; } = "LinkedIn";
    public string JobRole { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string JobDescription { get; set; } = "";
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> Responsibilities { get; set; } = new();
    public string ContactType { get; set; } = "recruiter";
    public string RecipientName { get; set; } = "";
    public string CandidateContext { get; set; } = "";
    public string Considerations { get; set; } = "";
    public string Language { get; set; } = "English";
    public string? Provider { get; set; }
    public string? Model { get; set; }
}

public class DirectMessageResultDto
{
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public string Channel { get; set; } = "LinkedIn";
    public string Language { get; set; } = "English";
}

public class DirectChatRequestDto
{
    public string System { get; set; } = "";
    public string User { get; set; } = "";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 1024;
    public string? Provider { get; set; }
    public string? Model { get; set; }
}

public class DirectChatResultDto
{
    public string Text { get; set; } = "";
}

public class LinkedInRequestDto
{
    public string Tool { get; set; } = "post"; // post | comment | message
    public string Language { get; set; } = "English";
    public string Tone { get; set; } = "professional";
    public string Length { get; set; } = "medium";
    public int Variants { get; set; } = 1;

    // post
    public string ContextType { get; set; } = "";
    public string Context { get; set; } = "";
    public List<string> Mentions { get; set; } = new();
    public bool IncludeHashtags { get; set; } = true;
    public int HashtagCount { get; set; } = 3;

    // comment
    public string TargetText { get; set; } = "";
    public List<string> Points { get; set; } = new();

    // message (non-apply)
    public string RecipientName { get; set; } = "";
    public string Relationship { get; set; } = "network";
    public string Purpose { get; set; } = "introduction";
    public string RecipientContext { get; set; } = "";
    public string SenderContext { get; set; } = "";

    public string? Provider { get; set; }
    public string? Model { get; set; }
}

public class LinkedInVariantDto
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Hashtags { get; set; } = "";
}

public class LinkedInResultDto
{
    public string Tool { get; set; } = "post";
    public List<LinkedInVariantDto> Variants { get; set; } = new();
}
