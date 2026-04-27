using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.src.config;
using backend.src.features.auth.interfaces;

namespace backend.src.features.auth.services;

public class KeycloakAdminService
{
    private readonly HttpClient _httpClient;

    public KeycloakAdminService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> GetAdminTokenAsync()
    {
        var formData = new Dictionary<string, string>
        {
            { "grant_type", "password" },
            { "client_id", KeycloakAdminConfig.AdminClientId },
            { "username", KeycloakAdminConfig.AdminUsername },
            { "password", KeycloakAdminConfig.AdminPassword }
        };

        var response = await _httpClient.PostAsync(
            KeycloakAdminConfig.TokenUrl,
            new FormUrlEncodedContent(formData));

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return json?.AccessToken;
    }

    public async Task<KeycloakUserCreationResult> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string password)
    {
        var token = await GetAdminTokenAsync();
        if (string.IsNullOrEmpty(token))
            return new KeycloakUserCreationResult { Success = false, Error = "Failed to obtain admin token" };

        var userPayload = new
        {
            email,
            firstName,
            lastName,
            username = email,
            enabled = true,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = password,
                    temporary = false
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, KeycloakAdminConfig.UsersUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(userPayload),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var location = response.Headers.Location?.ToString();
            var userId = ExtractUserIdFromLocation(location);
            return new KeycloakUserCreationResult { Success = true, UserId = userId };
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        return new KeycloakUserCreationResult { Success = false, Error = errorContent };
    }

    private static string? ExtractUserIdFromLocation(string? location)
    {
        if (string.IsNullOrEmpty(location))
            return null;
        var parts = location.Split('/');
        return parts.Length > 0 ? parts[^1] : null;
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
    }
}
