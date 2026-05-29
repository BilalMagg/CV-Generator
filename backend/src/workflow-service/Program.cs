using Microsoft.EntityFrameworkCore;
using WorkflowService;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8084, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenAnyIP(18084, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddDbContext<WorkflowDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, o => o.UseVector());
});

builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddGrpc();

// Old untyped client & generic service
builder.Services.AddHttpClient();
builder.Services.AddScoped<WorkflowService.Services.WorkflowExecutionService>();

// New Strongly-Typed Agent SDK Clients
builder.Services.AddHttpClient<WorkflowService.AgentClients.IJobExtractorClient, WorkflowService.AgentClients.JobExtractorClient>(client =>
{
    client.BaseAddress = new Uri("http://cv-job-extractor:8001/api/v1/");
});

builder.Services.AddHttpClient<WorkflowService.AgentClients.ISearchAgentClient, WorkflowService.AgentClients.SearchAgentClient>(client =>
{
    client.BaseAddress = new Uri("http://cv-search-agent:8002/api/v1/");
});

builder.Services.AddHttpClient<WorkflowService.AgentClients.ITemplateAgentClient, WorkflowService.AgentClients.TemplateAgentClient>(client =>
{
    client.BaseAddress = new Uri("http://cv-template-agent:8003/api/v1/");
});

builder.Services.AddHttpClient<WorkflowService.AgentClients.ICvOptimizerClient, WorkflowService.AgentClients.CvOptimizerClient>(client =>
{
    client.BaseAddress = new Uri("http://cv-optimizer:8004/api/v1/");
});

builder.Services.AddHttpClient<WorkflowService.AgentClients.IContactAgentClient, WorkflowService.AgentClients.ContactAgentClient>(client =>
{
    client.BaseAddress = new Uri("http://cv-contact-agent:8005/api/v1/");
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
                new WorkflowService.Entity.AgentEntity { AgentId = "job-extractor",  Name = "Job Extractor",  Role = "Job Description Parser",              BackgroundGradient = "linear-gradient(145deg, #0c2340 0%, #1a3a5c 50%, #2d6a9f 100%)", SortOrder = 1 },
                new WorkflowService.Entity.AgentEntity { AgentId = "search-agent",   Name = "Search Agent",   Role = "Smart Application Search",           BackgroundGradient = "linear-gradient(145deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%)", SortOrder = 2 },
                new WorkflowService.Entity.AgentEntity { AgentId = "template-agent", Name = "Template Agent", Role = "CV & Resume Generator",              BackgroundGradient = "linear-gradient(145deg, #1b1b2f 0%, #2d1b4e 50%, #4a1942 100%)", SortOrder = 3 },
                new WorkflowService.Entity.AgentEntity { AgentId = "cv-optimizer",   Name = "CV Optimizer",   Role = "Tailored CV Enhancer",               BackgroundGradient = "linear-gradient(145deg, #0d2818 0%, #1a3c2a 50%, #2d6b4a 100%)", SortOrder = 4 },
                new WorkflowService.Entity.AgentEntity { AgentId = "contact-agent",  Name = "Contact Agent",  Role = "Application Delivery",               BackgroundGradient = "linear-gradient(145deg, #2d0a28 0%, #4a154b 50%, #7b2d6b 100%)", SortOrder = 5 }
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