using CommonProtos.Application;
using Grpc.Core;

namespace NotificationService.Grpc;

public interface IApplicationGrpcClientService
{
    Task<ApplicationProto?> GetApplicationAsync(Guid applicationId, CancellationToken ct = default);
}

public class ApplicationGrpcClientService : IApplicationGrpcClientService
{
    private readonly ApplicationServiceGrpc.ApplicationServiceGrpcClient _client;
    private readonly ILogger<ApplicationGrpcClientService> _logger;

    public ApplicationGrpcClientService(
        ApplicationServiceGrpc.ApplicationServiceGrpcClient client,
        ILogger<ApplicationGrpcClientService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ApplicationProto?> GetApplicationAsync(Guid applicationId, CancellationToken ct = default)
    {
        try
        {
            return await _client.GetApplicationByIdAsync(
                new GetApplicationByIdRequest { Id = applicationId.ToString() },
                cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Application {Id} not found via gRPC", applicationId);
            return null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC call GetApplicationById failed for {Id}", applicationId);
            return null;
        }
    }
}
