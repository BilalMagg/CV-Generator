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
