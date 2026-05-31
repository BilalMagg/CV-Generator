using Confluent.Kafka;
using NotificationService.Application.Interfaces;
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
                // Topics are auto-created by producers on first publish — this is expected on startup
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
                var userId = GetString(doc, "userId") ?? GetString(doc, "candidateId");
                var company = GetString(doc, "companyName") ?? GetString(doc, "company");
                var position = GetString(doc, "positionTitle") ?? GetString(doc, "position");
                if (!Guid.TryParse(userId, out var uid) || company is null || position is null)
                {
                    _logger.LogWarning("application.created message missing required fields, skipping. Raw: {Json}", json);
                    return;
                }
                var userInfo = await userGrpc.GetUserInfoAsync(uid);
                if (userInfo is null)
                {
                    _logger.LogWarning("application.created: could not fetch user {UserId} from gRPC, skipping", uid);
                    return;
                }
                await notificationSvc.SendApplicationCreatedAsync(uid, userInfo.Value.Email, userInfo.Value.FirstName, company, position);
                break;
            }

            case KafkaTopics.ApplicationStatusChanged:
            {
                var userId = GetString(doc, "userId") ?? GetString(doc, "candidateId");
                var company = GetString(doc, "companyName") ?? GetString(doc, "company");
                var position = GetString(doc, "positionTitle") ?? GetString(doc, "position");
                var newStatus = GetString(doc, "newStatus");
                if (!Guid.TryParse(userId, out var uid) || company is null || position is null || newStatus is null)
                {
                    _logger.LogWarning("application.status.updated message missing required fields, skipping. Raw: {Json}", json);
                    return;
                }
                var userInfo = await userGrpc.GetUserInfoAsync(uid);
                if (userInfo is null)
                {
                    _logger.LogWarning("application.status.updated: could not fetch user {UserId} from gRPC, skipping", uid);
                    return;
                }
                await notificationSvc.SendApplicationStatusChangedAsync(uid, userInfo.Value.Email, userInfo.Value.FirstName, company, position, newStatus);
                break;
            }

            default:
                _logger.LogWarning("Unhandled topic: {Topic}", topic);
                break;
        }
    }

    // Returns null (instead of throwing) when the property is absent.
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