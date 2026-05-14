�
H/app/src/notification-service/API/Controllers/NotificationsController.cs�using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/notification")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationSvc;

    public NotificationsController(INotificationService notificationSvc)
    {
        _notificationSvc = notificationSvc;
    }

    /// <summary>Get all notifications for a user (in-app inbox)</summary>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserNotifications(Guid userId)
    {
        var notifications = await _notificationSvc.GetUserNotificationsAsync(userId);
        return Ok(notifications);
    }

    /// <summary>Mark a notification as read</summary>
    [HttpPatch("{notificationId}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        await _notificationSvc.MarkAsReadAsync(notificationId);
        return NoContent();
    }

    /// <summary>Manual trigger for testing — remove in production</summary>
    [HttpPost("test/welcome")]
    public async Task<IActionResult> TestWelcome([FromBody] TestWelcomeRequest req)
    {
        await _notificationSvc.SendWelcomeAsync(req.UserId, req.Email, req.FirstName);
        return Ok(new { message = "Welcome email triggered" });
    }
}

public record TestWelcomeRequest(Guid UserId, string Email, string FirstName);ParseOptions.0.json�
G/app/src/notification-service/Application/DTOs/NotificationResultDto.cs�using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public class NotificationResultDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}ParseOptions.0.json�
E/app/src/notification-service/Application/DTOs/SendNotificationDto.cs�namespace NotificationService.Application.DTOs;

public class SendNotificationDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
}ParseOptions.0.json�
E/app/src/notification-service/Application/Interfaces/IEmailService.cs�namespace NotificationService.Application.Interfaces;

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
}ParseOptions.0.json�
L/app/src/notification-service/Application/Interfaces/INotificationService.cs�using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

public interface INotificationService
{
    Task SendWelcomeAsync(Guid userId, string email, string firstName);
    Task SendCvGeneratedAsync(Guid userId, string email, string firstName, string cvDownloadUrl);
    Task SendApplicationCreatedAsync(Guid userId, string email, string firstName, string company, string position);
    Task SendApplicationStatusChangedAsync(Guid userId, string email, string firstName, string company, string position, string newStatus);
    Task SendNoResponseReminderAsync(Guid userId, string email, string firstName, string company, string position, int daysSinceApplied);
    Task SendWeeklyDigestAsync(Guid userId, string email, string firstName, int totalApplications, int responses, int cvsGenerated);
    Task<List<NotificationResultDto>> GetUserNotificationsAsync(Guid userId);
    Task MarkAsReadAsync(Guid notificationId);
}ParseOptions.0.json�
I/app/src/notification-service/Application/Interfaces/ITemplateRenderer.cs�namespace NotificationService.Application.Interfaces;

public interface ITemplateRenderer
{
    Task<string> RenderAsync(string templateName, object model);
}ParseOptions.0.json�
B/app/src/notification-service/Application/Services/EmailService.cs�using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using NotificationService.Application.Interfaces;

namespace NotificationService.Application.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var smtpSection = _config.GetSection("Smtp");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            smtpSection["FromName"],
            smtpSection["FromEmail"]
        ));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                smtpSection["Host"],
                int.Parse(smtpSection["Port"]!),
                SecureSocketOptions.StartTls
            );
            await client.AuthenticateAsync(smtpSection["Username"], smtpSection["Password"]);
            await client.SendAsync(message);
            _logger.LogInformation("Email sent to {Email} | Subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }
}ParseOptions.0.json�.
I/app/src/notification-service/Application/Services/NotificationService.cs�-using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IEmailService _emailService;
    private readonly ITemplateRenderer _renderer;
    private readonly NotificationDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IEmailService emailService,
        ITemplateRenderer renderer,
        NotificationDbContext db,
        ILogger<NotificationService> logger)
    {
        _emailService = emailService;
        _renderer = renderer;
        _db = db;
        _logger = logger;
    }

    public async Task SendWelcomeAsync(Guid userId, string email, string firstName)
    {
        var body = await _renderer.RenderAsync("welcome", new { first_name = firstName });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.Welcome,
            "Welcome to CV Generator 🎉", body);
    }

    public async Task SendCvGeneratedAsync(Guid userId, string email, string firstName, string cvDownloadUrl)
    {
        var body = await _renderer.RenderAsync("cv-ready", new
        {
            first_name = firstName,
            download_url = cvDownloadUrl
        });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.CvGenerated,
            "Your CV is ready to download!", body);
    }

    public async Task SendApplicationCreatedAsync(Guid userId, string email, string firstName, string company, string position)
    {
        var body = await _renderer.RenderAsync("application-created", new
        {
            first_name = firstName,
            company,
            position
        });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.ApplicationCreated,
            $"Application sent to {company} ✅", body);
    }

    public async Task SendApplicationStatusChangedAsync(Guid userId, string email, string firstName, string company, string position, string newStatus)
    {
        var body = await _renderer.RenderAsync("status-changed", new
        {
            first_name = firstName,
            company,
            position,
            status = newStatus
        });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.ApplicationStatusChanged,
            $"Your application at {company} has been updated", body);
    }

    public async Task SendNoResponseReminderAsync(Guid userId, string email, string firstName, string company, string position, int daysSinceApplied)
    {
        var body = await _renderer.RenderAsync("application-reminder", new
        {
            first_name = firstName,
            company,
            position,
            days = daysSinceApplied
        });
        var type = daysSinceApplied >= 14
            ? NotificationType.ApplicationNoResponseTwoWeeks
            : NotificationType.ApplicationNoResponseOneWeek;

        await SaveAndSendAsync(userId, email, firstName, type,
            $"No reply from {company} after {daysSinceApplied} days — time to follow up?", body);
    }

    public async Task SendWeeklyDigestAsync(Guid userId, string email, string firstName,
        int totalApplications, int responses, int cvs)
    {
        var body = await _renderer.RenderAsync("weekly-digest", new
        {
            first_name = firstName,
            total_applications = totalApplications,
            responses,
            cvs_generated = cvs
        });
        await SaveAndSendAsync(userId, email, firstName, NotificationType.WeeklyDigest,
            "Your weekly CV Generator summary 📊", body);
    }

    public async Task<List<NotificationResultDto>> GetUserNotificationsAsync(Guid userId)
    {
        return _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationResultDto
            {
                Id = n.Id,
                Type = n.Type,
                Subject = n.Subject,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToList();
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await _db.Notifications.FindAsync(notificationId);
        if (notification is null) return;
        notification.IsRead = true;
        await _db.SaveChangesAsync();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task SaveAndSendAsync(Guid userId, string email, string name,
        NotificationType type, string subject, string htmlBody)
    {
        var entity = new Notification
        {
            UserId = userId,
            UserEmail = email,
            Type = type,
            Channel = NotificationChannel.Email,
            Subject = subject,
            Body = htmlBody
        };

        _db.Notifications.Add(entity);

        try
        {
            await _emailService.SendAsync(email, name, subject, htmlBody);
            entity.IsSent = true;
            entity.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            entity.IsSent = false;
            entity.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Notification of type {Type} failed for user {UserId}", type, userId);
        }

        await _db.SaveChangesAsync();
    }
}ParseOptions.0.json�
F/app/src/notification-service/Application/Services/TemplateRenderer.cs�using NotificationService.Application.Interfaces;
using Scriban;

namespace NotificationService.Application.Services;

public class TemplateRenderer : ITemplateRenderer
{
    private readonly string _templatesPath;

    public TemplateRenderer()
    {
        _templatesPath = Path.Combine(AppContext.BaseDirectory, "Templates");
    }

    public async Task<string> RenderAsync(string templateName, object model)
    {
        var filePath = Path.Combine(_templatesPath, $"{templateName}.html");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Template '{templateName}' not found at {filePath}");

        var templateContent = await File.ReadAllTextAsync(filePath);
        var template = Template.Parse(templateContent);
        return await template.RenderAsync(model);
    }
}ParseOptions.0.json�
=/app/src/notification-service/Domain/Entities/Notification.cs�using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public bool IsSent { get; set; } = false;
    public string? ErrorMessage { get; set; }      
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}ParseOptions.0.json�
A/app/src/notification-service/Domain/Enums/NotificationChannel.csknamespace NotificationService.Domain.Enums;

public enum NotificationChannel
{
    Email,
    InApp
}ParseOptions.0.json�
>/app/src/notification-service/Domain/Enums/NotificationType.cs�namespace NotificationService.Domain.Enums;

public enum NotificationType
{
    // Account
    Welcome,
    EmailVerification,
    PasswordReset,
    PasswordChanged,

    // CV
    CvGenerated,
    CvExportReady,

    // Applications
    ApplicationCreated,
    ApplicationStatusChanged,
    ApplicationNoResponseOneWeek,
    ApplicationNoResponseTwoWeeks,

    // Profile
    ProfileIncomplete,
    ProfileInactive,

    // Digest
    WeeklyDigest
}ParseOptions.0.json�#
N/app/src/notification-service/Infrastructure/Messaging/KafkaConsumerService.cs�"using Confluent.Kafka;
using NotificationService.Application.Interfaces;
using System.Text.Json;

namespace NotificationService.Infrastructure.Messaging;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KafkaConsumerService> _logger;

    public KafkaConsumerService(IConfiguration config, IServiceScopeFactory scopeFactory,
        ILogger<KafkaConsumerService> logger)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kafkaConfig = new ConsumerConfig
        {
            BootstrapServers = _config["Kafka:BootstrapServers"],
            GroupId = _config["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var topics = new[]
        {
            KafkaTopics.UserCreated,
            KafkaTopics.CvGenerated,
            KafkaTopics.ApplicationCreated,
            KafkaTopics.ApplicationStatusChanged
        };

        using var consumer = new ConsumerBuilder<string, string>(kafkaConfig).Build();
        consumer.Subscribe(topics);

        _logger.LogInformation("Kafka consumer started, listening to topics: {Topics}", string.Join(", ", topics));

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
        var notificationSvc = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var doc = JsonDocument.Parse(json).RootElement;

        switch (topic)
        {
            case KafkaTopics.UserCreated:
                await notificationSvc.SendWelcomeAsync(
                    Guid.Parse(doc.GetProperty("userId").GetString()!),
                    doc.GetProperty("email").GetString()!,
                    doc.GetProperty("firstName").GetString()!
                );
                break;

            case KafkaTopics.CvGenerated:
                await notificationSvc.SendCvGeneratedAsync(
                    Guid.Parse(doc.GetProperty("userId").GetString()!),
                    doc.GetProperty("email").GetString()!,
                    doc.GetProperty("firstName").GetString()!,
                    doc.GetProperty("downloadUrl").GetString()!
                );
                break;

            case KafkaTopics.ApplicationCreated:
                await notificationSvc.SendApplicationCreatedAsync(
                    Guid.Parse(doc.GetProperty("userId").GetString()!),
                    doc.GetProperty("email").GetString()!,
                    doc.GetProperty("firstName").GetString()!,
                    doc.GetProperty("company").GetString()!,
                    doc.GetProperty("position").GetString()!
                );
                break;

            case KafkaTopics.ApplicationStatusChanged:
                await notificationSvc.SendApplicationStatusChangedAsync(
                    Guid.Parse(doc.GetProperty("userId").GetString()!),
                    doc.GetProperty("email").GetString()!,
                    doc.GetProperty("firstName").GetString()!,
                    doc.GetProperty("company").GetString()!,
                    doc.GetProperty("position").GetString()!,
                    doc.GetProperty("newStatus").GetString()!
                );
                break;

            default:
                _logger.LogWarning("Unhandled Kafka topic: {Topic}", topic);
                break;
        }
    }
}ParseOptions.0.json�
E/app/src/notification-service/Infrastructure/Messaging/KafkaTopics.cs�namespace NotificationService.Infrastructure.Messaging;

public static class KafkaTopics
{
    public const string UserCreated = "user.created";
    public const string CvGenerated = "cv.generated";
    public const string ApplicationCreated = "application.created";
    public const string ApplicationStatusChanged = "application.status.changed";
}ParseOptions.0.json�
Q/app/src/notification-service/Infrastructure/Persistence/NotificationDbContext.cs�using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Channel).HasConversion<string>();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}ParseOptions.0.json�
1/app/src/notification-service/Jobs/ReminderJob.cs�using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Jobs;

public class ReminderJob
{
    private readonly NotificationDbContext _db;
    private readonly INotificationService _notificationSvc;
    private readonly ILogger<ReminderJob> _logger;

    public ReminderJob(NotificationDbContext db, INotificationService notificationSvc,
        ILogger<ReminderJob> logger)
    {
        _db = db;
        _notificationSvc = notificationSvc;
        _logger = logger;
    }

    /// <summary>
    /// Called daily by Hangfire — checks for applications with no response
    /// and fires reminders at 7 and 14 days.
    /// NOTE: This job queries the notification history to know which applications
    /// already had reminders sent, to avoid duplicates.
    /// The actual application data comes from the application-tracking-service
    /// (in Phase 2 you'll call it via gRPC; for now inject it or use a shared DB view).
    /// </summary>
    public async Task CheckAndSendRemindersAsync()
    {
        _logger.LogInformation("Running application reminder job at {Time}", DateTime.UtcNow);

        // In microservices phase: call application-tracking-service via gRPC
        // to get list of pending applications per user.
        // For now this is a placeholder showing the logic clearly.

        // Example shape of what you'd get from application-tracking-service:
        // { UserId, Email, FirstName, Company, Position, DaysSinceApplied }

        // var pendingApplications = await _appTrackingGrpcClient.GetPendingAsync();

        // foreach (var app in pendingApplications)
        // {
        //     if (app.DaysSinceApplied == 7 || app.DaysSinceApplied == 14)
        //     {
        //         bool alreadySent = _db.Notifications.Any(n =>
        //             n.UserId == app.UserId &&
        //             n.Type == (app.DaysSinceApplied == 7
        //                 ? NotificationType.ApplicationNoResponseOneWeek
        //                 : NotificationType.ApplicationNoResponseTwoWeeks));
        //
        //         if (!alreadySent)
        //             await _notificationSvc.SendNoResponseReminderAsync(...);
        //     }
        // }

        _logger.LogInformation("Reminder job completed");
    }
}ParseOptions.0.json�
H/app/src/notification-service/Migrations/20260504152651_InitialCreate.cs�using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace notification_service.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEmail = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    IsSent = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}
ParseOptions.0.json�
Q/app/src/notification-service/Migrations/20260504152651_InitialCreate.Designer.cs�// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NotificationService.Infrastructure.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace notification_service.Migrations
{
    [DbContext(typeof(NotificationDbContext))]
    [Migration("20260504152651_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("NotificationService.Domain.Entities.Notification", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Body")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Channel")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<bool>("IsRead")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsSent")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("SentAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Subject")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("UserEmail")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("UserId");

                    b.ToTable("Notifications");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.json�
N/app/src/notification-service/Migrations/NotificationDbContextModelSnapshot.cs�// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NotificationService.Infrastructure.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace notification_service.Migrations
{
    [DbContext(typeof(NotificationDbContext))]
    partial class NotificationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("NotificationService.Domain.Entities.Notification", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Body")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Channel")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<bool>("IsRead")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsSent")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("SentAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Subject")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("UserEmail")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("UserId");

                    b.ToTable("Notifications");
                });
#pragma warning restore 612, 618
        }
    }
}
ParseOptions.0.json�
(/app/src/notification-service/Program.cs�using Hangfire;
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
app.Run();ParseOptions.0.json�
V/app/src/notification-service/obj/Debug/net10.0/notification-service.GlobalUsings.g.cs�// <auto-generated/>
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Net.Http.Json;
global using System.Threading;
global using System.Threading.Tasks;
ParseOptions.0.json�
_/app/src/notification-service/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs�// <autogenerated />
using System;
using System.Reflection;
[assembly: global::System.Runtime.Versioning.TargetFrameworkAttribute(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
ParseOptions.0.json�
T/app/src/notification-service/obj/Debug/net10.0/notification-service.AssemblyInfo.cs�//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: System.Reflection.AssemblyCompanyAttribute("notification-service")]
[assembly: System.Reflection.AssemblyConfigurationAttribute("Debug")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0")]
[assembly: System.Reflection.AssemblyProductAttribute("notification-service")]
[assembly: System.Reflection.AssemblyTitleAttribute("notification-service")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json�
g/app/src/notification-service/obj/Debug/net10.0/notification-service.MvcApplicationPartsAssemblyInfo.cs�//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

[assembly: Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartAttribute("Swashbuckle.AspNetCore.SwaggerGen")]

// Generated by the MSBuild WriteCodeFragment class.

ParseOptions.0.json�
�/app/src/notification-service/obj/Debug/net10.0/Microsoft.AspNetCore.App.SourceGenerators/Microsoft.AspNetCore.SourceGenerators.PublicProgramSourceGenerator/PublicTopLevelProgram.Generated.g.cs�// <auto-generated />
/// <summary>
/// Auto-generated public partial Program class for top-level statement apps.
/// </summary>
public partial class Program { }ParseOptions.0.json