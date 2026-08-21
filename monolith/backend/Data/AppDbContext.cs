using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CV_Generator.Models;

namespace CV_Generator.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<CVProfile> CVProfiles => Set<CVProfile>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Education> Educations => Set<Education>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<SocialLink> SocialLinks => Set<SocialLink>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<Hackathon> Hackathons => Set<Hackathon>();
    public DbSet<AcademicActivity> AcademicActivities => Set<AcademicActivity>();
    public DbSet<Cv> Cvs => Set<Cv>();
    public DbSet<CvVersion> CvVersions => Set<CvVersion>();
    public DbSet<CvSection> CvSections => Set<CvSection>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<ApplicationConfiguration> ApplicationConfigurations => Set<ApplicationConfiguration>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
    public DbSet<JobResponsibility> JobResponsibilities => Set<JobResponsibility>();
    public DbSet<JobBenefit> JobBenefits => Set<JobBenefit>();
    public DbSet<SearchCache> SearchCaches => Set<SearchCache>();
    public DbSet<UserQuota> UserQuotas => Set<UserQuota>();
    public DbSet<SearchJobMatch> SearchJobMatches => Set<SearchJobMatch>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<GmailConnection> GmailConnections => Set<GmailConnection>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<EmailSchedule> EmailSchedules => Set<EmailSchedule>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<AgentDocumentChunk> AgentDocumentChunks => Set<AgentDocumentChunk>();
    public DbSet<CvGenerationRun> CvGenerationRuns => Set<CvGenerationRun>();
    public DbSet<JobExtractionEntity> JobExtractions => Set<JobExtractionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Role).HasConversion<string>();
        });

        modelBuilder.Entity<Cv>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasMany(e => e.Versions).WithOne(e => e.Cv).HasForeignKey(e => e.CvId);
        });

        modelBuilder.Entity<CvVersion>(entity =>
        {
            entity.HasIndex(e => e.CvId);
            entity.HasMany(e => e.Sections).WithOne(e => e.Version).HasForeignKey(e => e.VersionId);
        });

        modelBuilder.Entity<Application>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.CandidateId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AppliedAt);
        });

        modelBuilder.Entity<ApplicationStatusHistory>(entity =>
        {
            entity.Property(e => e.OldStatus).HasConversion<string>();
            entity.Property(e => e.NewStatus).HasConversion<string>();
        });

        modelBuilder.Entity<ApplicationConfiguration>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
        });

        modelBuilder.Entity<JobOffer>(entity =>
        {
            entity.HasMany(j => j.Skills).WithOne(s => s.JobOffer).HasForeignKey(s => s.JobOfferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(j => j.Responsibilities).WithOne(r => r.JobOffer).HasForeignKey(r => r.JobOfferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(j => j.Benefits).WithOne(b => b.JobOffer).HasForeignKey(b => b.JobOfferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(j => j.JobHash).IsUnique().HasFilter("\"JobHash\" IS NOT NULL");
        });

        modelBuilder.Entity<SearchCache>(entity =>
        {
            entity.HasKey(s => s.SearchId);
            entity.HasIndex(s => new { s.Keyword, s.CrawledDate });
        });

        modelBuilder.Entity<UserQuota>(entity =>
        {
            entity.HasKey(u => u.UserId);
        });

        modelBuilder.Entity<SearchJobMatch>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => m.SearchId);
            entity.HasIndex(m => m.JobId);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Channel).HasConversion<string>();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.ReminderOffset).HasConversion<string>();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.ReminderAt, e.Status });
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
        });

        modelBuilder.Entity<GmailConnection>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Email);
        });

        modelBuilder.Entity<EmailMessage>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ContactId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.Contact)
                .WithMany()
                .HasForeignKey(e => e.ContactId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EmailSchedule>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.IsActive, e.NextRunAt });
            entity.Property(e => e.RecipientIds)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>())
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<AgentDocumentChunk>(entity =>
        {
            entity.HasGeneratedTsVectorColumn(
                c => c.SearchVector,
                "english",
                c => new { c.Content })
                .HasIndex(c => c.SearchVector)
                .HasMethod("GIN");
        });
    }
}
