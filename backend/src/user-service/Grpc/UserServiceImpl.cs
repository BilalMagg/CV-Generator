using CommonProtos.User;
using Microsoft.EntityFrameworkCore;
using UserService;
using UserService.Entity;

namespace UserService.Grpc;

public class UserServiceImpl : CommonProtos.User.UserServiceGrpc.UserServiceGrpcBase
{
    private readonly UserDbContext _db;
    private readonly ILogger<UserServiceImpl> _logger;

    public UserServiceImpl(UserDbContext db, ILogger<UserServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<UserProto> GetUserById(GetUserByIdRequest request, Grpc.Core.ServerCallContext context)
    {
        var user = await _db.Users.FindAsync(Guid.Parse(request.Id));
        if (user == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "User not found"));

        return ToProto(user);
    }

    public override async Task<UserProto> GetUserByEmail(GetUserByEmailRequest request, Grpc.Core.ServerCallContext context)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "User not found"));

        return ToProto(user);
    }

    public override async Task<UserProto> GetUserByKeycloakId(GetUserByKeycloakIdRequest request, Grpc.Core.ServerCallContext context)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == request.KeycloakId);
        if (user == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "User not found"));

        return ToProto(user);
    }

    public override async Task<UserExistsResponse> UserExists(UserExistsRequest request, Grpc.Core.ServerCallContext context)
    {
        var exists = await _db.Users.AnyAsync(u => u.Id == Guid.Parse(request.Id));
        return new UserExistsResponse { Exists = exists };
    }

    public override async Task<UserProto> CreateUser(CreateUserRequest request, Grpc.Core.ServerCallContext context)
    {
        var user = new User
        {
            KeycloakId = request.KeycloakId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Role = Enum.Parse<Role>(request.Role),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created user {Id} via gRPC", user.Id);
        return ToProto(user);
    }

    public override async Task<UserProto> UpdateUser(UpdateUserRequest request, Grpc.Core.ServerCallContext context)
    {
        var user = await _db.Users.FindAsync(Guid.Parse(request.Id));
        if (user == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "User not found"));

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.AvatarUrl = request.AvatarUrl;
        user.PreferencesJson = request.PreferencesJson;

        await _db.SaveChangesAsync();
        return ToProto(user);
    }

    public override async Task<DeleteUserResponse> DeleteUser(DeleteUserRequest request, Grpc.Core.ServerCallContext context)
    {
        var user = await _db.Users.FindAsync(Guid.Parse(request.Id));
        if (user == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "User not found"));

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return new DeleteUserResponse { Success = true };
    }

    private static UserProto ToProto(User u) => new()
    {
        Id = u.Id.ToString(),
        KeycloakId = u.KeycloakId,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        PhoneNumber = u.PhoneNumber ?? "",
        BirthDate = u.BirthDate?.ToString("O") ?? "",
        Role = u.Role.ToString(),
        AvatarUrl = u.AvatarUrl ?? "",
        CreatedAt = u.CreatedAt.ToString("O"),
        LastLogin = u.LastLogin?.ToString("O") ?? "",
        IsActive = u.IsActive,
        AiProfileDataJson = u.AiProfileDataJson ?? "",
        PreferencesJson = u.PreferencesJson ?? ""
    };
}