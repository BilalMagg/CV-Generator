using System.Text.Json.Serialization;

namespace CV_Generator.Dto;

public class BimeChatRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("conversation_id")]
    public Guid? ConversationId { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

public class BimeChatResponse
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";

    [JsonPropertyName("conversation_id")]
    public Guid? ConversationId { get; set; }
}

public class BimeConversationSummaryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("message_count")]
    public int MessageCount { get; set; }
}

public class BimeMessageDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class BimeConversationDetailDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<BimeMessageDto> Messages { get; set; } = new();
}

public class BimeCreateConversationRequest
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "New conversation";
}

public class BimeAddMessagesRequest
{
    [JsonPropertyName("messages")]
    public List<BimeMessageDto> Messages { get; set; } = new();
}
