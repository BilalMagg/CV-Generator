using Microsoft.EntityFrameworkCore;
using WorkflowService.Entity;

namespace WorkflowService;

public class WorkflowDbContext : DbContext
{
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<AgentDocumentChunk> AgentDocumentChunks { get; set; }
    public DbSet<Experience> Experiences { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<Skill> Skills { get; set; }

    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Register the pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // Automatically generate the tsvector for Full-Text Search based on the Content
        modelBuilder.Entity<AgentDocumentChunk>()
            .HasGeneratedTsVectorColumn(
                c => c.SearchVector,
                "english",
                c => new { c.Content })
            .HasIndex(c => c.SearchVector)
            .HasMethod("GIN");
    }
}