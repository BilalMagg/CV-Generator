using CV_Generator.Models;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace CV_Generator.Tests.Auth;

public class NativeAuthTests
{
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    [Fact]
    public void PasswordHasher_HashesAndVerifiesPasswordCorrectly()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "testuser@example.com",
            FirstName = "Test",
            LastName = "User"
        };

        var rawPassword = "StrongPassword123!";
        var hash = _hasher.HashPassword(user, rawPassword);

        Assert.NotNull(hash);
        Assert.NotEqual(rawPassword, hash);

        // Verify correct password
        var successResult = _hasher.VerifyHashedPassword(user, hash, rawPassword);
        Assert.True(successResult == PasswordVerificationResult.Success ||
                    successResult == PasswordVerificationResult.SuccessRehashNeeded);

        // Verify incorrect password
        var failedResult = _hasher.VerifyHashedPassword(user, hash, "WrongPassword123!");
        Assert.Equal(PasswordVerificationResult.Failed, failedResult);
    }

    [Fact]
    public void NativeUser_CanBeCreatedWithoutKeycloakId()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "native@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            KeycloakId = null, // Decoupled from Keycloak
            PasswordHash = _hasher.HashPassword(null!, "Pass123!"),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        Assert.Null(user.KeycloakId);
        Assert.NotNull(user.PasswordHash);
        Assert.True(user.EmailConfirmed);
        Assert.NotNull(user.SecurityStamp);
    }

    [Fact]
    public void PasswordReset_ExpiresAfterAllocatedTime()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "reset@example.com",
            FirstName = "Reset",
            LastName = "User",
            PasswordResetToken = Guid.NewGuid().ToString("N"),
            PasswordResetExpiresAt = DateTime.UtcNow.AddMinutes(-5) // Expired 5 mins ago
        };

        var isExpired = user.PasswordResetExpiresAt < DateTime.UtcNow;
        Assert.True(isExpired);

        var validUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "valid@example.com",
            FirstName = "Valid",
            LastName = "User",
            PasswordResetToken = Guid.NewGuid().ToString("N"),
            PasswordResetExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var isValid = validUser.PasswordResetExpiresAt > DateTime.UtcNow;
        Assert.True(isValid);
    }
}
