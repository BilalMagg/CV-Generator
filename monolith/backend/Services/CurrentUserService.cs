using System.Security.Claims;
using CV_Generator.Data;
using CV_Generator.Models;
using Microsoft.EntityFrameworkCore;

namespace CV_Generator.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly Lazy<Guid?> _userId;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext db, IEventBus eventBus, ILogger<CurrentUserService> logger)
    {
        _userId = new Lazy<Guid?>(() => Resolve(httpContextAccessor.HttpContext, db, eventBus, logger));
    }

    private static Guid? Resolve(HttpContext? ctx, AppDbContext db, IEventBus eventBus, ILogger<CurrentUserService> logger)
    {
        if (ctx == null) return null;

        var headerId = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerId) && Guid.TryParse(headerId, out var hid))
        {
            return hid;
        }

        var userIdClaim = ctx.User.FindFirstValue("user_id");
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var uid))
        {
            return uid;
        }

        var sub = ctx.User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(sub)) return null;

        var user = db.Users.FirstOrDefault(u => u.KeycloakId == sub);
        if (user != null)
        {
            return user.Id;
        }

        // Keycloak sub may have changed (account recreated with the same email).
        // Adopt the existing row by email instead of creating a duplicate.
        var email = ctx.User.FindFirstValue("email") ?? "";
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = db.Users.FirstOrDefault(u => u.Email.ToLower() == email.Trim().ToLower());
            if (byEmail != null)
            {
                byEmail.KeycloakId = sub;
                db.SaveChanges();
                return byEmail.Id;
            }
        }

        user = new User
        {
            KeycloakId = sub,
            FirstName = ctx.User.FindFirstValue("given_name") ?? "",
            LastName = ctx.User.FindFirstValue("family_name") ?? "",
            Email = email,
            Role = Role.USER,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.Users.Add(user);
        db.SaveChanges();

        logger.LogInformation("Auto-created user {Id} from JWT sub={Sub}", user.Id, sub);
        eventBus.PublishAsync(new UserCreatedEvent(user.Id, user.Email, user.FirstName, user.LastName));

        return user.Id;
    }

    public Guid? UserId => _userId.Value;
}
