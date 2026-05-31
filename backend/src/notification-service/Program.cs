using CommonProtos.User;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Auth;
using NotificationService.Infrastructure.GrpcClients;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Jobs;
using Serilog;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ── Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Core Services ──────────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAesEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<IGmailAuthService, GmailAuthService>();
builder.Services.AddScoped<IGmailSendService, GmailSendService>();
builder.Services.AddScoped<ITemplateRenderer, TemplateRenderer>();
builder.Services.AddScoped<INotificationService, NotificationService.Application.Services.NotificationService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<EmailScheduleService>();
builder.Services.AddScoped<ReminderJob>();
builder.Services.AddScoped<UserReminderJob>();
builder.Services.AddScoped<EmailScheduleJob>();

// ── HttpClient ─────────────────────────────────────────────────────────────
builder.Services.AddHttpClient();

// ── Kafka Consumers (Background Services) ──────────────────────────────────
// ── gRPC Clients ───────────────────────────────────────────────────────────
var userServiceUrl = builder.Configuration["GrpcClients:UserService"] ?? "http://cv-user-service:18082";
builder.Services.AddGrpcClient<UserServiceGrpc.UserServiceGrpcClient>(o =>
    o.Address = new Uri(userServiceUrl));
builder.Services.AddScoped<IUserGrpcClientService, UserGrpcClientService>();

// ── Kafka Consumer (Background Service) ────────────────────────────────────
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddHostedService<EmailSendRequestedConsumer>();

// ── Hangfire (Background Jobs) ─────────────────────────────────────────────
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(o =>
    {
        o.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    }));
builder.Services.AddHangfireServer();

// ── Auth ───────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(XUserIdAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, XUserIdAuthenticationHandler>(XUserIdAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

// ── API ────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Auto-migrate DB ────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    var pending = db.Database.GetPendingMigrations().ToList();
    Log.Information("Pending migrations: {Count} → {Migrations}", pending.Count, pending);
    db.Database.Migrate();
    Log.Information("Database migration completed successfully.");
}

// ── Hangfire Dashboard + Recurring Jobs ───────────────────────────────────
app.UseHangfireDashboard("/hangfire");

var cronExpression = builder.Configuration["Hangfire:ReminderCheckCronExpression"]!;
RecurringJob.AddOrUpdate<ReminderJob>(
    "application-reminders",
    job => job.CheckAndSendRemindersAsync(),
    cronExpression
);

var userReminderCron = builder.Configuration["Hangfire:UserReminderCronExpression"] ?? "*/1 * * * *";
RecurringJob.AddOrUpdate<UserReminderJob>(
    "user-reminders",
    job => job.ProcessAsync(),
    userReminderCron
);

RecurringJob.AddOrUpdate<EmailScheduleJob>(
    "email-schedules",
    job => job.ExecuteAsync(),
    "*/5 * * * *"
);

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();