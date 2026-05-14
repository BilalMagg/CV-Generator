using CommonProtos.User;
using Grpc.Core;
using Grpc.Net.Client;

namespace ApplicationService.Services;

public interface IUserGrpcClientService
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default);
    Task<UserProto?> GetUserAsync(Guid userId, CancellationToken ct = default);
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

    public async Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.UserExistsAsync(new UserExistsRequest { Id = userId.ToString() }, cancellationToken: ct);
            return response.Exists;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC call UserExists failed for {UserId}", userId);
            return false;
        }
    }

    public async Task<UserProto?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            return await _client.GetUserByIdAsync(new GetUserByIdRequest { Id = userId.ToString() }, cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC call GetUserById failed for {UserId}", userId);
            return null;
        }
    }
}
