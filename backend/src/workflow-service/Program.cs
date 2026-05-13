using Microsoft.EntityFrameworkCore;
using WorkflowService;

var builder = WebApplication.CreateBuilder(args);

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
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration failed");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapControllers();
app.MapGrpcService<WorkflowService.Grpc.WorkflowServiceImpl>();

app.Run();