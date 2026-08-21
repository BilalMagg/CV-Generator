using Microsoft.EntityFrameworkCore;
using ApplicationService.Entities;

namespace ApplicationService;

public class ApplicationDbContext : DbContext
{
    public DbSet<Application> Applications { get; set; }
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory { get; set; }
    public DbSet<ApplicationConfiguration> ApplicationConfigurations { get; set; }
    public DbSet<ApplicationAttempt> ApplicationAttempts { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Application>()
            .Property(a => a.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Application>()
            .Property(a => a.Origin)
            .HasConversion<string>();

        modelBuilder.Entity<ApplicationStatusHistory>()
            .Property(h => h.OldStatus)
            .HasConversion<string>();

        modelBuilder.Entity<ApplicationStatusHistory>()
            .Property(h => h.NewStatus)
            .HasConversion<string>();

        modelBuilder.Entity<Application>()
            .HasIndex(a => a.CandidateId);

        modelBuilder.Entity<Application>()
            .HasIndex(a => a.Status);

        modelBuilder.Entity<Application>()
            .HasIndex(a => a.AppliedAt);

        modelBuilder.Entity<Application>()
            .HasIndex(a => a.Fingerprint);

        // Hard rule: one application per (user, job offer)
        modelBuilder.Entity<Application>()
            .HasIndex(a => new { a.CandidateId, a.JobOfferId })
            .IsUnique()
            .HasFilter("\"JobOfferId\" IS NOT NULL");

        modelBuilder.Entity<ApplicationAttempt>(entity =>
        {
            entity.Property(x => x.Channel).HasConversion<string>();
            entity.Property(x => x.InitiatedBy).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>();

            entity.HasOne(x => x.Application)
                .WithMany(a => a.Attempts)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.ApplicationId, x.AttemptNumber })
                .IsUnique();

            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<ApplicationConfiguration>()
            .HasIndex(c => c.UserId)
            .IsUnique();
    }
}
