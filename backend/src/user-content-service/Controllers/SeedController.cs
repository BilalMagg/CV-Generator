using Bogus;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;

namespace UserContentService.Controllers;


[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly ContentDbContext _db;
    private readonly ILogger<SeedController> _logger;

    public SeedController(ContentDbContext db, ILogger<SeedController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Seed([FromBody] SeedRequest? request)
    {
        var r = request ?? new SeedRequest();
        var userId = r.UserId ?? Guid.NewGuid();
        var locale = "en";

        var experiences = new Faker<Experience>(locale)
            .RuleFor(e => e.Id, _ => Guid.NewGuid())
            .RuleFor(e => e.Title, f => f.Name.JobTitle())
            .RuleFor(e => e.Company, f => f.Company.CompanyName())
            .RuleFor(e => e.Description, f => f.Lorem.Paragraph())
            .RuleFor(e => e.StartDate, f => f.Date.Past(10).ToUniversalTime())
            .RuleFor(e => e.EndDate, (f, e) => f.Random.Bool(0.7f) ? f.Date.Soon(0, e.StartDate.AddYears(f.Random.Int(1, 4))).ToUniversalTime() : null)
            .RuleFor(e => e.Status, f => f.PickRandom("Completed", "Ongoing"))
            .RuleFor(e => e.UserId, userId)
            .Generate(r.ExperienceCount);

        var skills = new Faker<Skill>(locale)
            .RuleFor(s => s.Id, _ => Guid.NewGuid())
            .RuleFor(s => s.Name, f => f.Commerce.Department())
            .RuleFor(s => s.Level, f => f.PickRandom("Beginner", "Intermediate", "Advanced", "Expert"))
            .RuleFor(s => s.YearsOfExperience, f => f.Random.Int(1, 15))
            .RuleFor(s => s.Category, f => f.PickRandom("Frontend", "Backend", "DevOps", "Data", "Mobile", "Soft Skill"))
            .RuleFor(s => s.UserId, userId)
            .Generate(r.SkillCount);

        var projects = new Faker<Project>(locale)
            .RuleFor(p => p.Id, _ => Guid.NewGuid())
            .RuleFor(p => p.Title, f => f.Hacker.Phrase())
            .RuleFor(p => p.Description, f => f.Lorem.Paragraph())
            .RuleFor(p => p.Role, f => f.PickRandom("Lead Developer", "Full Stack Developer", "Frontend Developer", "Backend Developer", "DevOps Engineer"))
            .RuleFor(p => p.StartDate, f => f.Date.Past(5).ToUniversalTime())
            .RuleFor(p => p.EndDate, (f, p) => f.Random.Bool(0.8f) ? f.Date.Soon(0, p.StartDate.AddMonths(f.Random.Int(2, 18))).ToUniversalTime() : null)
            .RuleFor(p => p.RepositoryUrl, f => $"https://github.com/user/{f.Hacker.Noun()}")
            .RuleFor(p => p.DemoUrl, (f, p) => f.Random.Bool(0.5f) ? $"https://{f.Internet.DomainName()}" : null)
            .RuleFor(p => p.Status, f => f.PickRandom("Completed", "Ongoing", "On Hold"))
            .RuleFor(p => p.UserId, userId)
            .RuleFor(p => p.SkillsJson, f => $"[\"{string.Join("\",\"", f.Make(f.Random.Int(2, 5), () => f.Commerce.Department()))}\"]")
            .Generate(r.ProjectCount);

        var educations = new Faker<Education>(locale)
            .RuleFor(e => e.Id, _ => Guid.NewGuid())
            .RuleFor(e => e.InstitutionName, f => f.Company.CompanyName())
            .RuleFor(e => e.DegreeType, f => f.PickRandom("Bachelor's", "Master's", "PhD", "Associate", "Diploma"))
            .RuleFor(e => e.FieldOfStudy, f => f.Name.JobArea())
            .RuleFor(e => e.Specialization, f => f.PickRandom("Machine Learning", "Software Engineering", "Data Science", "Cybersecurity", "Cloud Computing", null))
            .RuleFor(e => e.StartDate, f => f.Date.Past(8).ToUniversalTime())
            .RuleFor(e => e.EndDate, (f, e) => f.Random.Bool(0.85f) ? f.Date.Soon(0, e.StartDate.AddYears(f.Random.Int(3, 5))).ToUniversalTime() : null)
            .RuleFor(e => e.Status, f => f.PickRandom("Completed", "Ongoing"))
            .RuleFor(e => e.City, f => f.Address.City())
            .RuleFor(e => e.UserId, userId)
            .Generate(r.EducationCount);

        var certifications = new Faker<Certification>(locale)
            .RuleFor(c => c.Id, _ => Guid.NewGuid())
            .RuleFor(c => c.Name, f => f.PickRandom("AWS Solutions Architect", "Google Cloud Professional", "Azure Administrator", "CISSP", "PMP", "Certified Kubernetes Administrator", "TOGAF", "ITIL Foundation"))
            .RuleFor(c => c.IssuingOrganization, f => f.PickRandom("Amazon", "Google", "Microsoft", "ISC2", "PMI", "CNCF", "The Open Group", "AXELOS"))
            .RuleFor(c => c.IssueDate, f => f.Date.Past(3).ToUniversalTime())
            .RuleFor(c => c.CredentialUrl, f => $"https://credential.example.com/{Guid.NewGuid()}")
            .RuleFor(c => c.UserId, userId)
            .Generate(r.CertificationCount);

        var socialLinks = new Faker<SocialLink>(locale)
            .RuleFor(s => s.Id, _ => Guid.NewGuid())
            .RuleFor(s => s.Platform, f => f.PickRandom("LinkedIn", "GitHub", "Twitter", "Medium", "Dev.to", "Stack Overflow"))
            .RuleFor(s => s.Url, f => $"https://{f.Internet.DomainName()}/{f.Internet.UserName()}")
            .RuleFor(s => s.UserId, userId)
            .Generate(r.SocialLinkCount);

        var interests = new Faker<Interest>(locale)
            .RuleFor(i => i.Id, _ => Guid.NewGuid())
            .RuleFor(i => i.Name, f => f.PickRandom("Open Source", "Machine Learning", "Cloud Computing", "Photography", "Travel", "Reading", "Chess", "Hiking", "Gaming", "Music"))
            .RuleFor(i => i.UserId, userId)
            .Generate(r.InterestCount);

        var languages = new Faker<Language>(locale)
            .RuleFor(l => l.Id, _ => Guid.NewGuid())
            .RuleFor(l => l.Name, f => f.PickRandom("English", "French", "Spanish", "German", "Arabic", "Mandarin", "Japanese", "Portuguese", "Italian", "Dutch"))
            .RuleFor(l => l.Level, f => f.PickRandom("Native", "Fluent", "Advanced", "Intermediate", "Beginner"))
            .RuleFor(l => l.UserId, userId)
            .Generate(r.LanguageCount);

        var hackathons = new Faker<Hackathon>(locale)
            .RuleFor(h => h.Id, _ => Guid.NewGuid())
            .RuleFor(h => h.Name, f => f.PickRandom("HackMIT", "TechCrunch Disrupt", "NASA Space Apps", "Devpost Global Hackathon", "MLH Fellowship", "ETHGlobal", "HackDavis"))
            .RuleFor(h => h.Organization, f => f.Company.CompanyName())
            .RuleFor(h => h.Date, f => f.Date.Past(3).ToUniversalTime())
            .RuleFor(h => h.Description, f => f.Lorem.Sentence())
            .RuleFor(h => h.Role, f => f.PickRandom("Participant", "Team Lead", "Mentor", "Judge"))
            .RuleFor(h => h.Result, f => f.PickRandom("Winner", "Finalist", "Top 10", "Participant", null))
            .RuleFor(h => h.UserId, userId)
            .Generate(r.HackathonCount);

        var academicActivities = new Faker<AcademicActivity>(locale)
            .RuleFor(a => a.Id, _ => Guid.NewGuid())
            .RuleFor(a => a.Title, f => f.PickRandom("Club President", "Volunteer Tutor", "Research Assistant", "Student Ambassador", "Peer Mentor", "Teaching Assistant"))
            .RuleFor(a => a.Organization, f => f.Company.CompanyName())
            .RuleFor(a => a.Description, f => f.Lorem.Sentence())
            .RuleFor(a => a.StartDate, f => f.Date.Past(4).ToUniversalTime())
            .RuleFor(a => a.EndDate, (f, a) => f.Random.Bool(0.7f) ? f.Date.Soon(0, a.StartDate.AddMonths(f.Random.Int(6, 24))).ToUniversalTime() : null)
            .RuleFor(a => a.UserId, userId)
            .Generate(r.AcademicActivityCount);

        var cvProfiles = new Faker<CVProfile>(locale)
            .RuleFor(c => c.Id, _ => Guid.NewGuid())
            .RuleFor(c => c.Title, f => $"{f.Name.JobTitle()} — {f.Company.CompanySuffix()}")
            .RuleFor(c => c.Summary, f => f.Lorem.Paragraph(3))
            .RuleFor(c => c.UserId, userId)
            .Generate(r.CvProfileCount);

        _db.Experiences.AddRange(experiences);
        _db.Skills.AddRange(skills);
        _db.Projects.AddRange(projects);
        _db.Educations.AddRange(educations);
        _db.Certifications.AddRange(certifications);
        _db.SocialLinks.AddRange(socialLinks);
        _db.Interests.AddRange(interests);
        _db.Languages.AddRange(languages);
        _db.Hackathons.AddRange(hackathons);
        _db.AcademicActivities.AddRange(academicActivities);
        _db.CVProfiles.AddRange(cvProfiles);

        await _db.SaveChangesAsync();

        var summary = new SeedSummary
        {
            UserId = userId,
            Experiences = experiences.Count,
            Skills = skills.Count,
            Projects = projects.Count,
            Educations = educations.Count,
            Certifications = certifications.Count,
            SocialLinks = socialLinks.Count,
            Interests = interests.Count,
            Languages = languages.Count,
            Hackathons = hackathons.Count,
            AcademicActivities = academicActivities.Count,
            CvProfiles = cvProfiles.Count,
            Total = experiences.Count + skills.Count + projects.Count + educations.Count
                  + certifications.Count + socialLinks.Count + interests.Count + languages.Count
                  + hackathons.Count + academicActivities.Count + cvProfiles.Count
        };

        _logger.LogInformation("Seeded {Total} records for User {UserId}", summary.Total, userId);
        return Ok(ApiResponse<SeedSummary>.Ok(summary));
    }

    [HttpDelete]
    public async Task<IActionResult> Clear([FromQuery] Guid? userId)
    {
        if (userId.HasValue)
        {
            await _db.Experiences.Where(e => e.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Skills.Where(s => s.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Projects.Where(p => p.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Educations.Where(e => e.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Certifications.Where(c => c.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.SocialLinks.Where(s => s.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Interests.Where(i => i.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Languages.Where(l => l.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Hackathons.Where(h => h.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.AcademicActivities.Where(a => a.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.CVProfiles.Where(c => c.UserId == userId.Value).ExecuteDeleteAsync();
        }
        else
        {
            _db.Experiences.RemoveRange(await _db.Experiences.ToListAsync());
            _db.Skills.RemoveRange(await _db.Skills.ToListAsync());
            _db.Projects.RemoveRange(await _db.Projects.ToListAsync());
            _db.Educations.RemoveRange(await _db.Educations.ToListAsync());
            _db.Certifications.RemoveRange(await _db.Certifications.ToListAsync());
            _db.SocialLinks.RemoveRange(await _db.SocialLinks.ToListAsync());
            _db.Interests.RemoveRange(await _db.Interests.ToListAsync());
            _db.Languages.RemoveRange(await _db.Languages.ToListAsync());
            _db.Hackathons.RemoveRange(await _db.Hackathons.ToListAsync());
            _db.AcademicActivities.RemoveRange(await _db.AcademicActivities.ToListAsync());
            _db.CVProfiles.RemoveRange(await _db.CVProfiles.ToListAsync());
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Cleared data for User {UserId}", userId?.ToString() ?? "ALL");
        return Ok(ApiResponse<object>.Ok(new { cleared = true, userId = userId?.ToString() ?? "all" }));
    }
}

public class SeedRequest
{
    public Guid? UserId { get; set; }
    public int ExperienceCount { get; set; } = 5;
    public int SkillCount { get; set; } = 10;
    public int ProjectCount { get; set; } = 4;
    public int EducationCount { get; set; } = 2;
    public int CertificationCount { get; set; } = 3;
    public int SocialLinkCount { get; set; } = 3;
    public int InterestCount { get; set; } = 5;
    public int LanguageCount { get; set; } = 2;
    public int HackathonCount { get; set; } = 2;
    public int AcademicActivityCount { get; set; } = 2;
    public int CvProfileCount { get; set; } = 1;
}

public class SeedSummary
{
    public Guid UserId { get; set; }
    public int Experiences { get; set; }
    public int Skills { get; set; }
    public int Projects { get; set; }
    public int Educations { get; set; }
    public int Certifications { get; set; }
    public int SocialLinks { get; set; }
    public int Interests { get; set; }
    public int Languages { get; set; }
    public int Hackathons { get; set; }
    public int AcademicActivities { get; set; }
    public int CvProfiles { get; set; }
    public int Total { get; set; }
}
