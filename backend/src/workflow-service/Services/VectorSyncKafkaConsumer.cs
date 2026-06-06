using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using WorkflowService.Entity;

namespace WorkflowService.Services;

public class VectorSyncKafkaConsumer : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VectorSyncKafkaConsumer> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public VectorSyncKafkaConsumer(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<VectorSyncKafkaConsumer> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _config["Kafka:BootstrapServers"];
        if (string.IsNullOrEmpty(bootstrapServers))
        {
            _logger.LogWarning("Kafka:BootstrapServers not configured, VectorSync consumer disabled");
            return;
        }

        var kafkaConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "vector-sync-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var topics = new[]
        {
            "experience-created", "experience-updated", "experience-deleted",
            "skill-created", "skill-updated", "skill-deleted",
            "project-created", "project-updated", "project-deleted"
        };

        using var consumer = new ConsumerBuilder<string, string>(kafkaConfig).Build();
        consumer.Subscribe(topics);

        _logger.LogInformation("VectorSync consumer started, listening to: {Topics}", string.Join(", ", topics));

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
                _logger.LogWarning("Topic not yet available: {Topic}", ex.ConsumerRecord?.Topic);
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
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

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

        try
        {
            switch (topic)
            {
                case "experience-created":
                case "experience-updated":
                    await HandleExperienceEvent(doc, db);
                    break;
                case "skill-created":
                case "skill-updated":
                    await HandleSkillEvent(doc, db);
                    break;
                case "project-created":
                case "project-updated":
                    await HandleProjectEvent(doc, db);
                    break;
                case "experience-deleted":
                case "skill-deleted":
                case "project-deleted":
                    await HandleDeleteEvent(doc, db);
                    break;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle event on topic {Topic}: {Json}", topic, json);
        }
    }

    private async Task HandleExperienceEvent(JsonElement doc, WorkflowDbContext db)
    {
        var id = GetGuid(doc, "Id");
        var userId = GetGuid(doc, "UserId");
        if (id is null || userId is null) return;

        var title = GetString(doc, "Title") ?? "";
        var company = GetString(doc, "Company") ?? "";
        var description = GetString(doc, "Description") ?? "";

        var content = $"Experience: {title}";
        if (!string.IsNullOrEmpty(company)) content += $" at {company}";
        if (!string.IsNullOrEmpty(description)) content += $". {description}";

        var embedding = await EmbedText(content);

        var existing = await db.AgentDocumentChunks
            .FirstOrDefaultAsync(x => x.SourceId == id.Value && x.SourceType == "experience");
        if (existing is not null)
            db.AgentDocumentChunks.Remove(existing);

        db.AgentDocumentChunks.Add(new AgentDocumentChunk
        {
            UserId = userId.Value,
            SourceType = "experience",
            SourceId = id.Value,
            Content = content,
            Embedding = new Vector(embedding)
        });

        _logger.LogInformation("Synced experience {Id} for user {UserId}", id, userId);
    }

    private async Task HandleSkillEvent(JsonElement doc, WorkflowDbContext db)
    {
        var id = GetGuid(doc, "Id");
        var userId = GetGuid(doc, "UserId");
        if (id is null || userId is null) return;

        var name = GetString(doc, "Name") ?? "";
        var level = GetString(doc, "Level") ?? "";

        var content = $"Skill: {name}";
        if (!string.IsNullOrEmpty(level)) content += $" (Level: {level})";

        var embedding = await EmbedText(content);

        var existing = await db.AgentDocumentChunks
            .FirstOrDefaultAsync(x => x.SourceId == id.Value && x.SourceType == "skill");
        if (existing is not null)
            db.AgentDocumentChunks.Remove(existing);

        db.AgentDocumentChunks.Add(new AgentDocumentChunk
        {
            UserId = userId.Value,
            SourceType = "skill",
            SourceId = id.Value,
            Content = content,
            Embedding = new Vector(embedding)
        });

        _logger.LogInformation("Synced skill {Id} for user {UserId}", id, userId);
    }

    private async Task HandleProjectEvent(JsonElement doc, WorkflowDbContext db)
    {
        var id = GetGuid(doc, "Id");
        var userId = GetGuid(doc, "UserId");
        if (id is null || userId is null) return;

        var title = GetString(doc, "Title") ?? "";
        var description = GetString(doc, "Description") ?? "";
        var achievements = GetString(doc, "Achievements") ?? "";

        var content = $"Project: {title}";
        if (!string.IsNullOrEmpty(description)) content += $". {description}";
        if (!string.IsNullOrEmpty(achievements)) content += $". Achievements: {achievements}";

        var embedding = await EmbedText(content);

        var existing = await db.AgentDocumentChunks
            .FirstOrDefaultAsync(x => x.SourceId == id.Value && x.SourceType == "project");
        if (existing is not null)
            db.AgentDocumentChunks.Remove(existing);

        db.AgentDocumentChunks.Add(new AgentDocumentChunk
        {
            UserId = userId.Value,
            SourceType = "project",
            SourceId = id.Value,
            Content = content,
            Embedding = new Vector(embedding)
        });

        _logger.LogInformation("Synced project {Id} for user {UserId}", id, userId);
    }

    private async Task HandleDeleteEvent(JsonElement doc, WorkflowDbContext db)
    {
        var id = GetGuid(doc, "Id");
        if (id is null) return;

        var existing = await db.AgentDocumentChunks
            .Where(x => x.SourceId == id.Value)
            .ToListAsync();

        db.AgentDocumentChunks.RemoveRange(existing);

        if (existing.Count > 0)
            _logger.LogInformation("Deleted {Count} vector chunks for source {Id}", existing.Count, id);
    }

    private async Task<float[]> EmbedText(string text)
    {
        var apiKey = _config["GOOGLE_API_KEY"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("GOOGLE_API_KEY not configured, returning empty embedding");
            return new float[768];
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent?key={apiKey}";

        var requestBody = new
        {
            model = "models/gemini-embedding-001",
            content = new { parts = new[] { new { text } } },
            outputDimensionality = 768
        };

        var httpClient = _httpClientFactory.CreateClient();
        var json = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(url, httpContent);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseJson);
        var values = doc.RootElement.GetProperty("embedding").GetProperty("values");

        var result = new float[values.GetArrayLength()];
        var i = 0;
        foreach (var v in values.EnumerateArray())
            result[i++] = v.GetSingle();

        return result;
    }

    private static Guid? GetGuid(JsonElement element, string propertyName)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return Guid.TryParse(prop.Value.GetString(), out var id) ? id : null;
        }
        return null;
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
