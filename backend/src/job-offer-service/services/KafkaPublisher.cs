using Confluent.Kafka;
using System.Text.Json;

namespace JobOfferService.Services;

public interface IKafkaPublisher
{
    Task PublishAsync(string topic, object payload);
}

public class KafkaPublisher : IKafkaPublisher, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaPublisher> _logger;

    public KafkaPublisher(IConfiguration config, ILogger<KafkaPublisher> logger)
    {
        _logger = logger;
        var bootstrapServers = config["Kafka:BootstrapServers"] ?? "kafka:9092";

        _producer = new ProducerBuilder<Null, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
        }).Build();

        _logger.LogInformation("KafkaPublisher connected to {Brokers}", bootstrapServers);
    }

    public async Task PublishAsync(string topic, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        });

        var result = await _producer.ProduceAsync(topic, new Message<Null, string> { Value = json });
        _logger.LogInformation(
            "Published to topic '{Topic}' partition {P} offset {O}",
            result.Topic, result.Partition, result.Offset);
    }

    public void Dispose() => _producer.Dispose();
}
