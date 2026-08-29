using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace CV_Generator.Services;

public class InternalServiceAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "InternalService";

    public InternalServiceAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override System.Threading.Tasks.Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(headerId) || !Guid.TryParse(headerId, out var userId))
            return System.Threading.Tasks.Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return System.Threading.Tasks.Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
