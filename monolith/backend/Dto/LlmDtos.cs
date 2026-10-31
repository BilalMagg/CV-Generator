using System.Text.Json.Serialization;

namespace CV_Generator.Dto;

public class LlmSettingsDto
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class LlmSettingsRequest
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
}

public class LlmProviderInfoDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("has_key")]
    public bool HasKey { get; set; }
}

public class LlmProvidersResponseDto
{
    [JsonPropertyName("providers")]
    public List<LlmProviderInfoDto> Providers { get; set; } = new();
}

public class LlmModelsResponseDto
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("models")]
    public List<string> Models { get; set; } = new();
}

public class UserLlmKeyDto
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("configured")]
    public bool Configured { get; set; }

    /// <summary>Masked hint e.g. "gsk_…3f2a" — never the plaintext.</summary>
    [JsonPropertyName("masked")]
    public string? Masked { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public class UserLlmKeysResponseDto
{
    [JsonPropertyName("keys")]
    public List<UserLlmKeyDto> Keys { get; set; } = new();
}

public class SetLlmKeyRequest
{
    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = "";
}

public class AgentLlmSettingDto
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = "";

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class AgentLlmSettingsRequest
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
}

/// Catalog view: every known agent with the user's saved provider/model (or null).
public class AgentLlmSettingsDto
{
    [JsonPropertyName("agents")]
    public List<AgentLlmSettingMapDto> Agents { get; set; } = new();
}

public class AgentLlmSettingMapDto
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}
