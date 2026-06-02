using Confluent.Kafka;
using System.Text.Json;
using JobOfferService.Entities;
using JobOfferService.Repositories;

namespace JobOfferService.Workers;

/// <summary>
/// Background worker that consumes messages from the 'crawl-summary' Kafka topic.
///
/// When the Python crawler finishes scraping, it publishes:
///   { "search_id": "...", "total_found": 20 }
///
/// This worker receives that message and:
///   1. Looks up (or creates) the SearchCache row for that SearchId.
///   2. Sets ExpectedCount = total_found.
///   3. Transitions Status from Pending → Extracting.
///
/// This is the "set the goal post" step of the Batch Counting Pattern.
/// </summary>
public class CrawlSummaryConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CrawlSummaryConsumer> _logger;

    private const string TopicName = "crawl-summary";
    private const string GroupId = "job-offer-service-summary-group";

    public CrawlSummaryConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<CrawlSummaryConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _config["Kafka:BootstrapServers"] ?? "kafka:9092";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true,
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        consumer.Subscribe(TopicName);

        _logger.LogInformation(
            "CrawlSummaryConsumer started — subscribed to topic '{Topic}'", TopicName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                _logger.LogInformation(
                    "Received crawl-summary message: {Value}", result.Message.Value);

                await HandleSummaryMessageAsync(result.Message.Value);
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error on topic '{Topic}'", TopicName);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CrawlSummaryConsumer");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        consumer.Close();
        _logger.LogInformation("CrawlSummaryConsumer stopped.");
    }

    private async Task HandleSummaryMessageAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("search_id", out var searchIdEl)
            || !root.TryGetProperty("total_found", out var totalFoundEl))
        {
            _logger.LogWarning("crawl-summary message missing required fields: {Json}", json);
            return;
        }

        if (!Guid.TryParse(searchIdEl.GetString(), out var searchId))
        {
            _logger.LogWarning("crawl-summary: invalid search_id value: {Val}", searchIdEl.GetString());
            return;
        }

        var totalFound = totalFoundEl.GetInt32();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISearchCacheRepository>();

        var cache = await repo.GetBySearchIdAsync(searchId);
        if (cache is null)
        {
            // Auto-create the row if the frontend didn't pre-create it
            cache = new SearchCache
            {
                SearchId = searchId,
                Keyword = "unknown",   // Will be overwritten if the frontend pre-created it
                Status = SearchStatus.Extracting,
                ExpectedCount = totalFound,
            };
            await repo.CreateAsync(cache);
            _logger.LogInformation(
                "Created SearchCache for SearchId {SearchId} | ExpectedCount={N}",
                searchId, totalFound);
        }
        else
        {
            cache.ExpectedCount = totalFound;
            cache.Status = SearchStatus.Extracting;
            await repo.UpdateAsync(cache);
            _logger.LogInformation(
                "Updated SearchCache for SearchId {SearchId} | ExpectedCount={N} Status=Extracting",
                searchId, totalFound);
        }
    }
}
