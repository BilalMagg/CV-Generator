using Confluent.Kafka;
using NotificationService.Application.Interfaces;
using NotificationService.Grpc;
using NotificationService.Infrastructure.GrpcClients;
using System.Text.Json;

namespace NotificationService.Infrastructure.Messaging;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KafkaConsumerService> _logger;

    public KafkaConsumerService(IConfiguration config, IServiceScopeFactory scopeFactory,
        ILogger<KafkaConsumerService> logger)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafkaConfig = new ConsumerConfig
        {
            BootstrapServers = _config["Kafka:BootstrapServers"],
            GroupId = _config["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var topics = new[]
        {
            KafkaTopics.UserCreated,
            KafkaTopics.CvGenerated,
            KafkaTopics.ApplicationCreated,
            KafkaTopics.ApplicationStatusChanged
        };

        using var consumer = new ConsumerBuilder<string, string>(kafkaConfig).Build();
        consumer.Subscribe(topics);

        _logger.LogInformation("Kafka consumer started, listening to topics: {Topics}", string.Join(", ", topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                await HandleMessageAsync(result.Topic, result.Message.Value);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                _logger.LogWarning("Topic not yet available: {Topic}. Retrying in 5s...", ex.ConsumerRecord?.Topic);
                await Task.Delay(5000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming Kafka message");
                await Task.Delay(1000, stoppingToken);
            }
        }

        consumer.Close();
    }

    private async Task HandleMessageAsync(string topic, string json)
    {
        using var scope = _scopeFactory.CreateScope();
        var notificationSvc = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var userGrpc = scope.ServiceProvider.GetRequiredService<IUserGrpcClientService>();
        var appGrpc = scope.ServiceProvider.GetRequiredService<IApplicationGrpcClientService>();

        JsonElement doc;
        try
        {
            doc = JsonDocument.Parse(json).RootElement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Kafka message JSON on topic {Topic}", topic);
            return;
        }

        switch (topic)
        {
            case KafkaTopics.UserCreated:
            {
                var userId = GetString(doc, "userId");
                var email = GetString(doc, "email");
                var firstName = GetString(doc, "firstName");
                if (!Guid.TryParse(userId, out var uid) || email is null || firstName is null)
                {
                    _logger.LogWarning("user.created message missing required fields, skipping. Raw: {Json}", json);
                    return;
                }
                await notificationSvc.SendWelcomeAsync(uid, email, firstName);
                break;
            }

            case KafkaTopics.CvGenerated:
            {
                var userId = GetString(doc, "userId");
                var downloadUrl = GetString(doc, "downloadUrl");
                if (!Guid.TryParse(userId, out var uid) || downloadUrl is null)
                {
                    _logger.LogWarning("cv.generated message missing required fields, skipping. Raw: {Json}", json);
                    return;
                }
                var userInfo = await userGrpc.GetUserInfoAsync(uid);
                if (userInfo is null)
                {
                    _logger.LogWarning("cv.generated: could not fetch user {UserId} from gRPC, skipping", uid);
                    return;
                }
                await notificationSvc.SendCvGeneratedAsync(uid, userInfo.Value.Email, userInfo.Value.FirstName, downloadUrl);
                break;
            }

            case KafkaTopics.ApplicationCreated:
            {
                var appIdStr = GetString(doc, "applicationId");
                if (!Guid.TryParse(appIdStr, out var appId))
                {
                    _logger.LogWarning("application.created message missing applicationId, skipping. Raw: {Json}", json);
                    return;
                }
                var app = await appGrpc.GetApplicationAsync(appId);
                if (app is null)
                {
                    _logger.LogWarning("application.created: could not fetch application {Id} from gRPC, skipping", appId);
                    return;
                }
                if (!Guid.TryParse(app.CandidateId, out var uid))
                {
                    _logger.LogWarning("application.created: invalid candidateId in application {Id}, skipping", appId);
                    return;
                }
                var userInfo = await userGrpc.GetUserInfoAsync(uid);
                if (userInfo is null)
                {
                    _logger.LogWarning("application.created: could not fetch user {UserId} from gRPC, skipping", uid);
                    return;
                }
                await notificationSvc.SendApplicationCreatedAsync(uid, userInfo.Value.Email, userInfo.Value.FirstName, app.CompanyName, app.PositionTitle);
                break;
            }

            case KafkaTopics.ApplicationStatusChanged:
            {
var appIdStr = GetString(doc, "applicationId") ?? GetString(doc, "ApplicationId");
var newStatus = GetString(doc, "newStatus") ?? GetString(doc, "NewStatus");
if (!Guid.TryParse(appIdStr, out var appId) || newStatus is null)
{
    _logger.LogWarning("application.status.updated message missing required fields, skipping. Raw: {Json}", json);
    return;
}
                var app = await appGrpc.GetApplicationAsync(appId);
                if (app is null)
                {
                    _logger.LogWarning("application.status.updated: could not fetch application {Id} from gRPC, skipping", appId);
                    return;
                }
                if (!Guid.TryParse(app.CandidateId, out var uid))
                {
                    _logger.LogWarning("application.status.updated: invalid candidateId in application {Id}, skipping", appId);
                    return;
                }
                var userInfo = await userGrpc.GetUserInfoAsync(uid);
                if (userInfo is null)
                {
                    _logger.LogWarning("application.status.updated: could not fetch user {UserId} from gRPC, skipping", uid);
                    return;
                }
                await notificationSvc.SendApplicationStatusChangedAsync(uid, userInfo.Value.Email, userInfo.Value.FirstName, app.CompanyName, app.PositionTitle, newStatus);
                break;
            }

            default:
                _logger.LogWarning("Unhandled topic: {Topic}", topic);
                break;
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return prop.Value.GetString();
        }
        return null;
    }
}
