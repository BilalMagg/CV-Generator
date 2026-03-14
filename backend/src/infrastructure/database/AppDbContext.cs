using Microsoft.EntityFrameworkCore;
using backend.src.features.user.entity;
using backend.src.features.auth.entity;
using backend.src.features.project.entity;


public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserToken> UserTokens { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<LoginAttempt> LoginAttempts { get; set; }
    public DbSet<Project> Projects { get; set;}

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

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
    }
}