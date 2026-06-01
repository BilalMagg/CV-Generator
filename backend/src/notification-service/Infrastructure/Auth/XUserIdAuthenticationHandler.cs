using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NotificationService.Infrastructure.Auth;

public class XUserIdAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "XUserId";

    public XUserIdAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var userIdValues))
            return Task.FromResult(AuthenticateResult.Fail("Missing X-User-Id header"));

        var userId = userIdValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out _))
            return Task.FromResult(AuthenticateResult.Fail("Invalid X-User-Id header"));

        var claims = new[] { new Claim("user_id", userId) };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
