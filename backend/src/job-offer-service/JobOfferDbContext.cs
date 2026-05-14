using Microsoft.EntityFrameworkCore;
using JobOfferService.Entities;
using Pgvector.EntityFrameworkCore; // Required for pgvector


public class JobOfferDbContext : DbContext
{
    public JobOfferDbContext(DbContextOptions<JobOfferDbContext> options) : base(options) { }

    public DbSet<JobOffer> JobOffers { get; set; }
    public DbSet<JobSkill> JobSkills { get; set; }
    public DbSet<JobResponsibility> JobResponsibilities { get; set; }
    public DbSet<JobBenefit> JobBenefits { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. CRITICAL: Tell PostgreSQL to enable the vector extension
        modelBuilder.HasPostgresExtension("vector");

        // 2. Configure the JobOffer table and its relationships
        modelBuilder.Entity<JobOffer>(entity =>
        {
            entity.HasMany(j => j.Skills).WithOne(s => s.JobOffer).HasForeignKey(s => s.JobOfferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(j => j.Responsibilities).WithOne(r => r.JobOffer).HasForeignKey(r => r.JobOfferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(j => j.Benefits).WithOne(b => b.JobOffer).HasForeignKey(b => b.JobOfferId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}