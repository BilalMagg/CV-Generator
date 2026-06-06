using CommonProtos.User;
using Grpc.Core;

namespace NotificationService.Infrastructure.GrpcClients;

public interface IUserGrpcClientService
{
    Task<(string Email, string FirstName)?> GetUserInfoAsync(Guid userId, CancellationToken ct = default);
}

public class UserGrpcClientService : IUserGrpcClientService
{
    private readonly UserServiceGrpc.UserServiceGrpcClient _client;
    private readonly ILogger<UserGrpcClientService> _logger;

    public UserGrpcClientService(UserServiceGrpc.UserServiceGrpcClient client, ILogger<UserGrpcClientService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(string Email, string FirstName)?> GetUserInfoAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var user = await _client.GetUserByIdAsync(
                new GetUserByIdRequest { Id = userId.ToString() },
                cancellationToken: ct);

            return (user.Email, user.FirstName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("User {UserId} not found via gRPC", userId);
            return null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC call failed for user {UserId}", userId);
            return null;
        }
    }
}
