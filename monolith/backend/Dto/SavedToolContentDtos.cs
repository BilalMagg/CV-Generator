namespace CV_Generator.Dto;

public class SavedToolContentDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Tool { get; set; } = "post"; // post | comment | message | emojify
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Hashtags { get; set; } = "";
    public string OriginalText { get; set; } = "";
    public bool IsAdjusted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateSavedToolContentDto
{
    public string Tool { get; set; } = "post";
    public string? Title { get; set; }
    public string Text { get; set; } = "";
    public string? Hashtags { get; set; }
}

public class UpdateSavedToolContentDto
{
    /// <summary>Nullable: absent = unchanged; "" = clear.</summary>
    public string? Title { get; set; }
    public string? Text { get; set; }
    public string? Hashtags { get; set; }
}