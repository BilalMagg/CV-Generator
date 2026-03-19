using Microsoft.EntityFrameworkCore;
using backend.src.features.user.entity;
using backend.src.features.auth.entity;
using backend.src.features.project.entity;
using backend.src.features.skill.entity;
using backend.src.features.experience.entity;
using Pgvector;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserToken> UserTokens { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<LoginAttempt> LoginAttempts { get; set; }
    public DbSet<Project> Projects { get; set;}
    public DbSet<Experience> Experiences { get; set;}
    public DbSet<Skill> Skills { get; set;}

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure pgvector type
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Configure vector columns for RAG search
        modelBuilder.Entity<Experience>()
            .Property(e => e.DescriptionEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Project>()
            .Property(p => p.DescriptionEmbedding)
            .HasColumnType("vector(384)");

        modelBuilder.Entity<Skill>()
            .Property(s => s.NameEmbedding)
            .HasColumnType("vector(384)");

        // Optional: additional constraints / relationships
        modelBuilder.Entity<UserToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tokens)
            .HasForeignKey(t => t.UserId);

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.PasswordResetTokens)
            .HasForeignKey(t => t.UserId);

        modelBuilder.Entity<LoginAttempt>()
            .HasOne(t => t.User)
            .WithMany(u => u.LoginAttempts)
            .HasForeignKey(t => t.UserId);

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