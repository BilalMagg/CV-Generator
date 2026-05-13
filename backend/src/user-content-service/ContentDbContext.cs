using Microsoft.EntityFrameworkCore;

using UserContentService.Entity;

namespace UserContentService;

public class ContentDbContext : DbContext
{
    public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

    public DbSet<Project> Projects { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<Experience> Experiences { get; set; }
    public DbSet<Education> Educations { get; set; }
    public DbSet<SocialLink> SocialLinks { get; set; }
    public DbSet<Interest> Interests { get; set; }
    public DbSet<Language> Languages { get; set; }
    public DbSet<Certification> Certifications { get; set; }
    public DbSet<CVProfile> CVProfiles { get; set; }
    public DbSet<Hackathon> Hackathons { get; set; }
    public DbSet<AcademicActivity> AcademicActivities { get; set; }
}