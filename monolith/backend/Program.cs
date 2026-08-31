using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Data;
using CV_Generator.Hubs;
using CV_Generator.Services;
using CV_Generator.Services.AgentClients;
using CV_Generator.Services.BackgroundServices;
using CV_Generator.Dto;

var builder = WebApplication.CreateBuilder(args);

// Load optional .env (git-ignored) from the backend project dir; real env vars win.
var envFile = Path.Combine(builder.Environment.ContentRootPath, ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
        var eq = trimmed.IndexOf('=');
        if (eq <= 0) continue;
        var key = trimmed[..eq].Trim();
        var value = trimmed[(eq + 1)..].Trim().Trim('"');
        if (Environment.GetEnvironmentVariable(key) is null)
            Environment.SetEnvironmentVariable(key, value);
    }
}

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
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, CV_Generator.Services.InternalServiceAuthHandler>(
        CV_Generator.Services.InternalServiceAuthHandler.SchemeName, options => { });
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            "Bearer", CV_Generator.Services.InternalServiceAuthHandler.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
});

// Event bus (in-process Kafka replacement)
builder.Services.AddSingleton<IEventBus, SynchronousEventBus>();

// HTTP clients for AI agents — all live in unified sidecar at :8000
var agentBase = Environment.GetEnvironmentVariable("AGENTS_URL") ?? "http://localhost:8000";

builder.Services.AddHttpClient<IJobExtractorClient, JobExtractorClient>(c => c.BaseAddress = new Uri($"{agentBase}/api/agents/extract/"));
builder.Services.AddHttpClient<ISearchAgentClient, SearchAgentClient>(c => c.BaseAddress = new Uri($"{agentBase}/api/agents/search/"));
builder.Services.AddHttpClient<ITemplateAgentClient, TemplateAgentClient>(c =>
{
    c.BaseAddress = new Uri($"{agentBase}/api/agents/template/");
    // LaTeX PDF compilation via docker texlive can take several minutes.
    c.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddHttpClient<ICvOptimizerClient, CvOptimizerClient>(c => c.BaseAddress = new Uri($"{agentBase}/api/agents/cv-optimizer/"));
builder.Services.AddHttpClient<IContactAgentClient, ContactAgentClient>(c => c.BaseAddress = new Uri($"{agentBase}/api/agents/contact/"));
builder.Services.AddHttpClient<CV_Generator.Services.AgentClients.IJobCrawlerClient, CV_Generator.Services.AgentClients.JobCrawlerClient>(c => c.BaseAddress = new Uri($"{agentBase}/api/agents/crawler/"));
builder.Services.AddHttpClient<IPdfThumbnailService, PdfThumbnailService>(c =>
{
    c.BaseAddress = new Uri($"{agentBase}/api/agents/pdf/");
    c.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient<IApplyPrepClient, ApplyPrepClient>(c =>
{
    c.BaseAddress = new Uri($"{agentBase}/api/agents/apply-prep/");
    c.Timeout = TimeSpan.FromMinutes(3);
});

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
builder.Services.AddScoped<ScheduleTemplateService>();
builder.Services.AddScoped<ApplyService>();
builder.Services.AddScoped<IApplyPrepService, ApplyPrepService>();
builder.Services.AddScoped<WorkflowExecutionService>();
builder.Services.AddScoped<TemplateRenderService>();
builder.Services.AddScoped<IBimeService, BimeService>();
builder.Services.AddScoped<ISearchSyncService, SearchSyncService>();
builder.Services.AddScoped<ILlmSettingsService, LlmSettingsService>();
builder.Services.AddScoped<IAgentLlmSettingsService, AgentLlmSettingsService>();
builder.Services.AddHttpClient("agents", c =>
{
    c.BaseAddress = new Uri("http://localhost:8000");
    c.Timeout = TimeSpan.FromMinutes(4);
});

    builder.Services.AddHttpClient<ICategorizationClient, CategorizationClient>(c => c.BaseAddress = new Uri($"{agentBase}/api/agents/categorize"));
    builder.Services.AddHttpClient<IAutofillClient, AutofillClient>(c => c.BaseAddress = new Uri($"{agentBase}/api/agents/autofill/"));
builder.Services.AddScoped<ICategoryService, CategoryService>();

// Background services
builder.Services.AddSingleton<CvGenerationBackgroundService>();
builder.Services.AddSingleton<TemplateRenderBackgroundService>();
builder.Services.AddSingleton<IMinioStorageService, MinioStorageService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CvGenerationBackgroundService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TemplateRenderBackgroundService>());
builder.Services.AddHostedService<EmailScheduleWorker>();

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

        try
        {
            await CategorySeed.SeedAsync(db);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Category seed failed");
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
