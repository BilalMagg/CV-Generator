using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Jobs;
using Serilog;

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
builder.Services.AddScoped<ITemplateRenderer, TemplateRenderer>();
builder.Services.AddScoped<INotificationService, NotificationService.Application.Services.NotificationService>();
builder.Services.AddScoped<ReminderJob>();

// ── Kafka Consumer (Background Service) ────────────────────────────────────
builder.Services.AddHostedService<KafkaConsumerService>();

// ── Hangfire (Background Jobs) ─────────────────────────────────────────────
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(o =>
    {
        o.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    }));
builder.Services.AddHangfireServer();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();