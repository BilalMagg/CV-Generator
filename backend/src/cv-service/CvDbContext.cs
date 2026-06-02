using Microsoft.EntityFrameworkCore;
using CvService.Entities;

namespace CvService;

public class CvDbContext : DbContext
{
    public CvDbContext(DbContextOptions<CvDbContext> options) : base(options) { }

    public DbSet<Cv> Cvs { get; set; }
    public DbSet<CvVersion> CvVersions { get; set; }
    public DbSet<CvSection> CvSections { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuration of entity relationships, indexes, and constraints
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
    }
}
