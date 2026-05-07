using Microsoft.EntityFrameworkCore;
using WorkflowService.Entity;

namespace WorkflowService;

public class WorkflowDbContext : DbContext
{
    public DbSet<Workflow> Workflows { get; set; }

    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}