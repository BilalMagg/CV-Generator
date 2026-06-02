using Confluent.Kafka;
using NotificationService.Application.Interfaces;
using System.Text.Json;

namespace NotificationService.Infrastructure.Messaging;

public class EmailSendRequestedConsumer : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailSendRequestedConsumer> _logger;

    public EmailSendRequestedConsumer(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailSendRequestedConsumer> logger)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _config["Kafka:BootstrapServers"],
            GroupId = $"{_config["Kafka:GroupId"]}-email-send",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(KafkaTopics.EmailSendRequested);

        _logger.LogInformation(
            "EmailSendRequested consumer started, listening to topic: {Topic}",
            KafkaTopics.EmailSendRequested);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                await HandleMessageAsync(result.Message.Value, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming email.send.requested message");
                await Task.Delay(1000, stoppingToken);
            }
        }

        consumer.Close();
    }

    private async Task HandleMessageAsync(string json, CancellationToken ct)
    {
        EmailSendRequestedEvent? evt;
        try
        {
            evt = JsonSerializer.Deserialize<EmailSendRequestedEvent>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse email.send.requested event: {Json}", json);
            return;
        }

        if (evt is null || evt.UserId == Guid.Empty || string.IsNullOrWhiteSpace(evt.To))
        {
            _logger.LogWarning("Invalid email.send.requested event: {Json}", json);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var gmailSendSvc = scope.ServiceProvider.GetRequiredService<IGmailSendService>();
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _config["Kafka:BootstrapServers"]
        };

        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        try
        {
            await gmailSendSvc.SendWithAttachmentAsync(
                evt.UserId, evt.To, evt.Subject, evt.Body, evt.CvPdfUrl);

            var sentEvent = new EmailSentEvent
            {
                UserId = evt.UserId,
                To = evt.To,
                Subject = evt.Subject,
                Success = true
            };

            producer.Produce(KafkaTopics.EmailSent,
                new Message<string, string>
                {
                    Key = evt.UserId.ToString(),
                    Value = JsonSerializer.Serialize(sentEvent)
                });

            _logger.LogInformation(
                "Email sent via Gmail to {To} for user {UserId}", evt.To, evt.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Gmail to {To} for user {UserId}", evt.To, evt.UserId);

            var failedEvent = new EmailSentEvent
            {
                UserId = evt.UserId,
                To = evt.To,
                Subject = evt.Subject,
                Success = false,
                Error = ex.Message
            };

            producer.Produce(KafkaTopics.EmailFailed,
                new Message<string, string>
                {
                    Key = evt.UserId.ToString(),
                    Value = JsonSerializer.Serialize(failedEvent)
                });
        }

        producer.Flush(ct);
    }

    private class EmailSendRequestedEvent
    {
        public Guid UserId { get; set; }
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? CvPdfUrl { get; set; }
    }

    private class EmailSentEvent
    {
        public Guid UserId { get; set; }
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
