using Microsoft.EntityFrameworkCore;
using Pgvector;
using UserContentService.Entity;

namespace UserContentService;

public class ContentDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<Experience> Experiences { get; set; }

    public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Project>()
            .Property(p => p.DescriptionEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Skill>()
            .Property(s => s.NameEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Experience>()
            .Property(e => e.DescriptionEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Project>()
            .HasOne(p => p.User)
            .WithMany(u => u.Projects)
            .HasForeignKey(p => p.UserId);

        modelBuilder.Entity<Skill>()
            .HasOne(s => s.User)
            .WithMany(u => u.Skills)
            .HasForeignKey(s => s.UserId);

        modelBuilder.Entity<Experience>()
            .HasOne(e => e.User)
            .WithMany(u => u.Experiences)
            .HasForeignKey(e => e.UserId);
    }
}