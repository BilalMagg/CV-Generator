using System.Text.Json;
using Confluent.Kafka;
using ApplicationService.Events;

namespace ApplicationService.Services;

public interface IKafkaPublisher
{
    Task PublishAsync<T>(T evt, string topic) where T : ApplicationEvent;
}

public class KafkaPublisher : IKafkaPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaPublisher> _logger;

    public KafkaPublisher(IConfiguration configuration, ILogger<KafkaPublisher> logger)
    {
        _logger = logger;

        var bootstrapServers = configuration.GetValue<string>("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092";

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.Leader,
            MessageTimeoutMs = 5000,
            RequestTimeoutMs = 5000,
            RetryBackoffMs = 100,
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<T>(T evt, string topic) where T : ApplicationEvent
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = GetKey(evt),
                Value = JsonSerializer.Serialize(evt),
                Headers = new Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes(typeof(T).Name) },
                    { "event-id", System.Text.Encoding.UTF8.GetBytes(evt.EventId.ToString()) }
                }
            };

            var deliveryResult = await _producer.ProduceAsync(topic, message);

            _logger.LogInformation("Published {EventType} to {Topic} [offset {Offset}]",
                typeof(T).Name, deliveryResult.Topic, deliveryResult.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish {EventType} to {Topic}: {Reason}",
                typeof(T).Name, topic, ex.Error.Reason);
        }
    }

    private static string GetKey(ApplicationEvent evt) => evt switch
    {
        ApplicationCreatedEvent e => e.ApplicationId.ToString(),
        ApplicationStatusUpdatedEvent e => e.ApplicationId.ToString(),
        ApplicationUpdatedEvent e => e.ApplicationId.ToString(),
        ApplicationDeletedEvent e => e.ApplicationId.ToString(),
        _ => evt.EventId.ToString()
    };

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
