using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Hubs;
using CV_Generator.Services;
using CV_Generator.Services.AgentClients;
using CV_Generator.Services.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000");
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=cv_monolith;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));

// Auth
var jwtAuthority = Environment.GetEnvironmentVariable("JWT_AUTHORITY")
    ?? builder.Configuration["JWT_AUTHORITY"]
    ?? "http://localhost:9090/realms/cv-realm";
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = jwtAuthority;
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateAudience = false;
    });
builder.Services.AddAuthorization();

// Event bus (in-process Kafka replacement)
builder.Services.AddSingleton<IEventBus, SynchronousEventBus>();

// HTTP clients for AI agents
var jobExtractorUrl = Environment.GetEnvironmentVariable("JOB_EXTRACTOR_URL") ?? "http://cv-job-extractor:8001/api/v1/";
var searchAgentUrl = Environment.GetEnvironmentVariable("SEARCH_AGENT_URL") ?? "http://cv-search-agent:8002/api/v1/";
var templateAgentUrl = Environment.GetEnvironmentVariable("TEMPLATE_AGENT_URL") ?? "http://cv-template-agent:8003/api/v1/";
var cvOptimizerUrl = Environment.GetEnvironmentVariable("CV_OPTIMIZER_URL") ?? "http://cv-optimizer:8004/api/v1/";
var contactAgentUrl = Environment.GetEnvironmentVariable("CONTACT_AGENT_URL") ?? "http://cv-contact-agent:8005/api/v1/";
var jobCrawlerUrl = Environment.GetEnvironmentVariable("JOB_CRAWLER_URL") ?? "http://cv-job-crawler:8006/api/v1/";
var jobOfferServiceUrl = Environment.GetEnvironmentVariable("JOB_OFFER_SERVICE_URL") ?? "http://cv-job-offer-service:8086";

builder.Services.AddHttpClient<IJobExtractorClient, JobExtractorClient>(c => c.BaseAddress = new Uri(jobExtractorUrl));
builder.Services.AddHttpClient<ISearchAgentClient, SearchAgentClient>(c => c.BaseAddress = new Uri(searchAgentUrl));
builder.Services.AddHttpClient<ITemplateAgentClient, TemplateAgentClient>(c => c.BaseAddress = new Uri(templateAgentUrl));
builder.Services.AddHttpClient<ICvOptimizerClient, CvOptimizerClient>(c => c.BaseAddress = new Uri(cvOptimizerUrl));
builder.Services.AddHttpClient<IContactAgentClient, ContactAgentClient>(c => c.BaseAddress = new Uri(contactAgentUrl));
builder.Services.AddHttpClient<CV_Generator.Services.AgentClients.IJobCrawlerClient, CV_Generator.Services.AgentClients.JobCrawlerClient>(c => c.BaseAddress = new Uri(jobCrawlerUrl));

// Notification services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAesEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<IGmailAuthService, GmailAuthService>();
builder.Services.AddScoped<IGmailSendService, GmailSendService>();
builder.Services.AddScoped<ITemplateRenderer, TemplateRenderer>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<EmailScheduleService>();
builder.Services.AddScoped<WorkflowExecutionService>();

// Background services
builder.Services.AddSingleton<CvGenerationBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CvGenerationBackgroundService>());

// SignalR
builder.Services.AddSignalR();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// User context resolution
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var pending = await db.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            logger.LogInformation("Applying {Count} pending migrations...", pending.Count());
            await db.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration failed");
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<JobHub>("/hubs/jobs");
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "cv-monolith" }));

app.Run();
