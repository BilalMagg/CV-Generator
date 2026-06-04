using Microsoft.EntityFrameworkCore;
using WorkflowService;
using WorkflowService.AgentClients;
using WorkflowService.Entity;
using WorkflowService.Services;

Console.WriteLine("[CV_GEN_2026-06-03] Starting WorkflowService with CvGenerationBackgroundService registration...");

var builder = WebApplication.CreateBuilder(args);

var httpPort = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "8084");
var grpcPort = int.Parse(Environment.GetEnvironmentVariable("GRPC_PORT") ?? "18084");

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(httpPort, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenAnyIP(grpcPort, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddDbContext<WorkflowDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, o => o.UseVector());
});

builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddGrpc();

// Old untyped client
builder.Services.AddHttpClient();

// Background job queue for CV generation
builder.Services.AddSingleton<CvGenerationBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CvGenerationBackgroundService>());
builder.Services.AddScoped<WorkflowExecutionService>();

// Kafka consumer for incremental vector sync from user-content-service events
builder.Services.AddHostedService<VectorSyncKafkaConsumer>();

// New Strongly-Typed Agent SDK Clients — URLs from env vars
var jobExtractorUrl = Environment.GetEnvironmentVariable("JOB_EXTRACTOR_URL") ?? "http://cv-job-extractor:8001/api/v1/";
var searchAgentUrl = Environment.GetEnvironmentVariable("SEARCH_AGENT_URL") ?? "http://cv-search-agent:8002/api/v1/";
var templateAgentUrl = Environment.GetEnvironmentVariable("TEMPLATE_AGENT_URL") ?? "http://cv-template-agent:8003/api/v1/";
var cvOptimizerUrl = Environment.GetEnvironmentVariable("CV_OPTIMIZER_URL") ?? "http://cv-optimizer:8004/api/v1/";
var contactAgentUrl = Environment.GetEnvironmentVariable("CONTACT_AGENT_URL") ?? "http://cv-contact-agent:8005/api/v1/";

builder.Services.AddHttpClient<IJobExtractorClient, JobExtractorClient>(client =>
{
    client.BaseAddress = new Uri(jobExtractorUrl);
});

builder.Services.AddHttpClient<ISearchAgentClient, SearchAgentClient>(client =>
{
    client.BaseAddress = new Uri(searchAgentUrl);
});

builder.Services.AddHttpClient<ITemplateAgentClient, TemplateAgentClient>(client =>
{
    client.BaseAddress = new Uri(templateAgentUrl);
});

builder.Services.AddHttpClient<ICvOptimizerClient, CvOptimizerClient>(client =>
{
    client.BaseAddress = new Uri(cvOptimizerUrl);
});

builder.Services.AddHttpClient<IContactAgentClient, ContactAgentClient>(client =>
{
    client.BaseAddress = new Uri(contactAgentUrl);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var pending = await dbContext.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            logger.LogInformation("Applying {Count} migrations...", pending.Count());
            await dbContext.Database.MigrateAsync();
        }

        var seeded = await dbContext.Agents.AnyAsync();
        if (!seeded)
        {
            logger.LogInformation("Seeding agent definitions...");
            dbContext.Agents.AddRange(
                new AgentEntity { AgentId = "job-extractor",  Name = "Job Extractor",  Role = "Job Description Parser",              BackgroundGradient = "linear-gradient(145deg, #0c2340 0%, #1a3a5c 50%, #2d6a9f 100%)", SortOrder = 1 },
                new AgentEntity { AgentId = "search-agent",   Name = "Search Agent",   Role = "Smart Application Search",           BackgroundGradient = "linear-gradient(145deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%)", SortOrder = 2 },
                new AgentEntity { AgentId = "template-agent", Name = "Template Agent", Role = "CV & Resume Generator",              BackgroundGradient = "linear-gradient(145deg, #1b1b2f 0%, #2d1b4e 50%, #4a1942 100%)", SortOrder = 3 },
                new AgentEntity { AgentId = "cv-optimizer",   Name = "CV Optimizer",   Role = "Tailored CV Enhancer",               BackgroundGradient = "linear-gradient(145deg, #0d2818 0%, #1a3c2a 50%, #2d6b4a 100%)", SortOrder = 4 },
                new AgentEntity { AgentId = "contact-agent",  Name = "Contact Agent",  Role = "Application Delivery",               BackgroundGradient = "linear-gradient(145deg, #2d0a28 0%, #4a154b 50%, #7b2d6b 100%)", SortOrder = 5 }
            );
            await dbContext.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration or seed failed");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapControllers();
app.MapGrpcService<WorkflowService.Grpc.WorkflowServiceImpl>();

app.Run();
