using Confluent.Kafka;
using NotificationService.Application.Interfaces;
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

        var doc = JsonDocument.Parse(json).RootElement;

        switch (topic)
        {
            case KafkaTopics.UserCreated:
                await notificationSvc.SendWelcomeAsync(
                    Guid.Parse(doc.GetProperty("userId").GetString()!),
                    doc.GetProperty("email").GetString()!,
                    doc.GetProperty("firstName").GetString()!
                );
                break;

            case KafkaTopics.CvGenerated:
                await notificationSvc.SendCvGeneratedAsync(
                    Guid.Parse(doc.GetProperty("userId").GetString()!),
                    doc.GetProperty("email").GetString()!,
                    doc.GetProperty("firstName").GetString()!,
                    doc.GetProperty("downloadUrl").GetString()!
                );
                break;

            case KafkaTopics.ApplicationCreated:
                await notificationSvc.SendApplicationCreatedAsync(
                    Guid.Parse(doc.GetProperty("userId").GetString()!),
                    doc.GetProperty("email").GetString()!,
                    doc.GetProperty("firstName").GetString()!,
                    doc.GetProperty("company").GetString()!,
                    doc.GetProperty("position").GetString()!
                );
                break;

            case KafkaTopics.ApplicationStatusChanged:
                await notificationSvc.SendApplicationStatusChangedAsync(
                    Guid.Parse(doc.GetProperty("userId").GetString()!),
                    doc.GetProperty("email").GetString()!,
                    doc.GetProperty("firstName").GetString()!,
                    doc.GetProperty("company").GetString()!,
                    doc.GetProperty("position").GetString()!,
                    doc.GetProperty("newStatus").GetString()!
                );
                break;

            default:
                _logger.LogWarning("Unhandled Kafka topic: {Topic}", topic);
                break;
        }
    }
}