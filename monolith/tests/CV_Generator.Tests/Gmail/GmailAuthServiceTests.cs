using CV_Generator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CV_Generator.Tests.Gmail;

public class GmailAuthServiceTests
{
    private readonly IConfiguration _config;
    private readonly ILogger<GmailAuthService> _logger;
    private readonly IAesEncryptionService _aes;
    private readonly IGmailTokenRefresher _refresher;
    private readonly IHttpClientFactory _httpClientFactory;

    public GmailAuthServiceTests()
    {
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", "test-google-client-id");
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET", "test-google-client-secret");

        var inMemory = new Dictionary<string, string?>
        {
            ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = "super-secret-key-that-is-at-least-32-chars-long!",
            ["Gmail:CallbackBaseUrl"] = "http://localhost:4200"
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        _logger = Substitute.For<ILogger<GmailAuthService>>();
        _aes = Substitute.For<IAesEncryptionService>();
        _refresher = Substitute.For<IGmailTokenRefresher>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
    }

    private GmailAuthService CreateService()
    {
        return new GmailAuthService(null!, _aes, _refresher, _httpClientFactory, _config, _logger);
    }

    [Fact]
    public void GenerateSignedState_GeneratesFourPartSignedToken()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        var state = service.GenerateSignedState(userId, "/mailbox");

        Assert.NotNull(state);
        var parts = state.Split('|');
        Assert.Equal(4, parts.Length);
        Assert.Equal(userId.ToString(), parts[0]);
    }

    [Fact]
    public void TryValidateState_ValidState_ReturnsTrueAndExtractsUserId()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        var state = service.GenerateSignedState(userId, "/mailbox");
        var isValid = service.TryValidateState(state, out var extractedUserId, out var returnUrl);

        Assert.True(isValid);
        Assert.Equal(userId, extractedUserId);
        Assert.Equal("/mailbox", returnUrl);
    }

    [Fact]
    public void TryValidateState_TamperedSignature_ReturnsFalse()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        var state = service.GenerateSignedState(userId, "/mailbox");
        var parts = state.Split('|');
        // Tamper with signature
        parts[3] = parts[3].Substring(0, parts[3].Length - 4) + "XXXX";
        var tamperedState = string.Join('|', parts);

        var isValid = service.TryValidateState(tamperedState, out var extractedUserId, out var returnUrl);

        Assert.False(isValid);
        Assert.Equal(Guid.Empty, extractedUserId);
    }

    [Fact]
    public void TryValidateState_TamperedPayload_ReturnsFalse()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var state = service.GenerateSignedState(userId, "/mailbox");
        var parts = state.Split('|');
        // Swap user id in payload
        parts[0] = anotherUserId.ToString();
        var tamperedState = string.Join('|', parts);

        var isValid = service.TryValidateState(tamperedState, out var extractedUserId, out var returnUrl);

        Assert.False(isValid);
        Assert.Equal(Guid.Empty, extractedUserId);
    }

    [Fact]
    public void TryValidateState_ExpiredTimestamp_ReturnsFalse()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();

        // 20 minutes ago (exceeds 15-minute window)
        var oldTime = DateTimeOffset.UtcNow.AddMinutes(-20).ToUnixTimeSeconds().ToString();
        var returnPart = Uri.EscapeDataString("/mailbox");
        var rawPayload = $"{userId}|{returnPart}|{oldTime}";

        var secret = "super-secret-key-that-is-at-least-32-chars-long!";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var sigBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawPayload));
        var sig = Convert.ToBase64String(sigBytes);
        var expiredState = $"{rawPayload}|{sig}";

        var isValid = service.TryValidateState(expiredState, out var extractedUserId, out var returnUrl);

        Assert.False(isValid);
        Assert.Equal(Guid.Empty, extractedUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-state")]
    [InlineData("part1|part2|part3")]
    public void TryValidateState_MalformedInput_ReturnsFalse(string malformed)
    {
        var service = CreateService();

        var isValid = service.TryValidateState(malformed, out var extractedUserId, out var returnUrl);

        Assert.False(isValid);
        Assert.Equal(Guid.Empty, extractedUserId);
    }
}
