using Microsoft.EntityFrameworkCore;
using ApplicationService.Entities;

namespace ApplicationService;

public class ApplicationDbContext : DbContext
{
    public DbSet<Application> Applications { get; set; }
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Application>()
            .Property(a => a.Status)
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
    }
}